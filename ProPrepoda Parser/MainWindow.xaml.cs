using System.Windows;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ProPrepoda_Parser.data;

namespace ProPrepoda_Parser;

public partial class MainWindow : Window
{
	public const string url = "https://proprepoda.com";
	private static IConfiguration _config = Configuration.Default.WithDefaultLoader();
	private static IBrowsingContext _context = BrowsingContext.New(_config);

	private int selected_city_index;

	City? selected_city;
	School? selected_school;
	Prepod? selected_prepod;

	public MainWindow()
	{
		InitializeComponent();

		cities.SelectionChanged += async (_, _) =>
		{
			prepod.Content = "";
			schools.ItemsSource = null;
			prepods.ItemsSource = null;

			if (cities.SelectedIndex == -1)
				return;

			selected_city_index = cities.SelectedIndex;
			var list = (List<string>)cities.ItemsSource;

			var hash_cities = await GetCities();
			var selected = cities.ItemsSource.OfType<string>().ToList()[selected_city_index];
			var selected_address = (selected_city = hash_cities.First(city => city.name == selected)).url;
			//MessageBox.Show(selected_address);
			var schools_list = (await GetSchools(selected_address)).Select(school => $"{school.name}{(school.full_name.Length > 0 ? " - " + school.full_name : "")}");
			schools.ItemsSource = schools_list;

			if (schools_list.Count() == 0)
				prepod.Content = "Пусто";
		};

		schools.SelectionChanged += async (_, _) =>
		{
			prepod.Content = "";
			if (schools.SelectedIndex == -1)
				return;

			selected_school = (await GetSchools(selected_city!.url)).ToList()[schools.SelectedIndex];
			var hash_prepods = await GetPrepods(selected_school.url);

			var prepods_list = hash_prepods.Select(prepod => prepod.fio).ToList();
			prepods.ItemsSource = prepods_list;

			if (prepods_list.Count() == 0)
				prepod.Content = "Пусто";
		};

		prepods.SelectionChanged += async (_, _) =>
		{
			if (prepods.SelectedIndex == -1)
				return;

			selected_prepod = (await GetPrepods(selected_school!.url)).ToList()[prepods.SelectedIndex];
			prepods.ItemsSource = (await GetPrepods(selected_school.url));

			prepod.Content = selected_prepod.ToString();
		};

		refresh();
	}

	async void refresh()
	{
		cities.ItemsSource = (await GetCities()).Select(city => city.name).ToList();
	}

	async ValueTask<HashSet<Prepod>> GetPrepods(string address)
	{
		var hash = new HashSet<Prepod>();

		var document = await _context.OpenAsync(address);
		var cellSelector = "div.prof-item";
		var cells = document.QuerySelectorAll(cellSelector);
		foreach (var cell in cells)
		{
			//MessageBox.Show(string.Join("\n", cell.Text().Split('\n').Select(t => t.Trim())));

			var href = cell.Children[0].Attributes["href"]!.Value;

			var fio = cell.TextContent.Split(cell.Children[1].TextContent)[0];
			if (fio.EndsWith(" - "))
				fio = fio[..^3].Trim();
			var other = cell.TextContent.Split(fio)[1];
			var split = other.Split("\n");
			var otdel = split[0];
			if(otdel.StartsWith(" - "))
				otdel = otdel[3..];

			var elements = cell.GetElementsByTagName("*");
			var plus = elements.Length == 5 ? 0 : 1;
			var comments = int.Parse(elements[2 + plus].TextContent);
			var rating = int.Parse(elements[4 + plus].TextContent);

			Prepod p;
			hash.Add(p = new Prepod(fio, otdel.Trim(), url + href, new Rating(comments, rating)));

			//MessageBox.Show(p.ToString());
		}

		return hash;
	}

	async ValueTask<HashSet<School>> GetSchools(string address)
	{
		var hash = new HashSet<School>();
		
		var document = await _context.OpenAsync(address);
		var cells = document.QuerySelectorAll("div.school-item");
		foreach (var cell in cells)
		{
			var a = cell.Children[0];
			var span = cell.Children[1];

			hash.Add(new School(a.TextContent, span.TextContent, url + a.Attributes["href"]!.Value));
		}

		return hash;
	}

	async ValueTask<HashSet<City>> GetCities()
	{
		var hash = new HashSet<City>();

		var document = await _context.OpenAsync(url);
		var cells = document.QuerySelector("div#city-list");

		foreach (var cell in cells!.Children)
		{
			var ul = cell.FirstElementChild;
			foreach (var li in ul.Children)
			{
				var city = li.TextContent;
				string href = li.FirstElementChild!.LocalName.ToLower().Equals("a")
					? li.FirstElementChild.Attributes["href"]!.Value
					: li.FirstElementChild.FirstElementChild!.Attributes["href"]!.Value;

				hash.Add(new City(city, url + href));
			}
		}

		return hash;
	}
}