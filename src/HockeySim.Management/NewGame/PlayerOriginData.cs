using HockeySim.Domain;

namespace HockeySim.Management.NewGame;

/// <summary>
/// The hockey nations players are drawn from: how common each nationality is, the given names
/// and family names that fit it, and the places players are born there. Names are combined at
/// random, so players are fictional even though the places are real.
/// </summary>
/// <remarks>
/// Weights approximate the nationality mix of recent NHL rosters and total 100.
/// </remarks>
internal static class PlayerOriginData
{
    public static IReadOnlyList<CountryOrigin> Countries { get; } =
    [
        new(
            Country.Canada,
            Weight: 41,
            FirstNames:
            [
                "Connor", "Nathan", "Brayden", "Tyler", "Ryan", "Mathieu", "Alexis", "Jordan",
                "Cole", "Brandon", "Dylan", "Evan", "Logan", "Kyle", "Owen", "Jake",
                "Mitchell", "Samuel", "Carter", "Liam",
            ],
            LastNames:
            [
                "MacDonald", "Tremblay", "Gagnon", "Campbell", "Thompson", "Fraser", "Bouchard", "Roy",
                "Bergeron", "Lavoie", "Stewart", "MacKenzie", "Gauthier", "Wilson", "Morrison", "Fortier",
                "Robertson", "Pelletier", "Sinclair", "Doherty",
            ],
            Birthplaces:
            [
                Place("Toronto", "Ontario"), Place("Mississauga", "Ontario"), Place("Ottawa", "Ontario"),
                Place("London", "Ontario"), Place("Hamilton", "Ontario"), Place("Sudbury", "Ontario"),
                Place("Kingston", "Ontario"), Place("Montreal", "Quebec"), Place("Quebec City", "Quebec"),
                Place("Sherbrooke", "Quebec"), Place("Laval", "Quebec"), Place("Winnipeg", "Manitoba"),
                Place("Brandon", "Manitoba"), Place("Regina", "Saskatchewan"), Place("Saskatoon", "Saskatchewan"),
                Place("Calgary", "Alberta"), Place("Edmonton", "Alberta"), Place("Red Deer", "Alberta"),
                Place("Vancouver", "British Columbia"), Place("Kelowna", "British Columbia"),
                Place("Victoria", "British Columbia"), Place("Halifax", "Nova Scotia"),
                Place("Moncton", "New Brunswick"), Place("St. John's", "Newfoundland and Labrador"),
                Place("Charlottetown", "Prince Edward Island"),
            ]),
        new(
            Country.UnitedStates,
            Weight: 27,
            FirstNames:
            [
                "Jack", "Brady", "Matthew", "Zach", "Kevin", "Patrick", "Chris", "Hunter",
                "Cooper", "Tanner", "Mason", "Wyatt", "Trevor", "Jason", "Luke", "Drew",
                "Brock", "Seth", "Tommy", "Ben",
            ],
            LastNames:
            [
                "Miller", "Johnson", "Sullivan", "Murphy", "Anderson", "Carlson", "Hayes", "Walsh",
                "Peterson", "Kowalski", "Brooks", "Fitzgerald", "Reilly", "Dunn", "Parker", "Shea",
                "Gallagher", "Olson", "Brennan", "Mitchell",
            ],
            Birthplaces:
            [
                Place("Boston", "Massachusetts"), Place("Detroit", "Michigan"), Place("Grand Rapids", "Michigan"),
                Place("Plymouth", "Michigan"), Place("Minneapolis", "Minnesota"), Place("Duluth", "Minnesota"),
                Place("Warroad", "Minnesota"), Place("Chicago", "Illinois"), Place("Buffalo", "New York"),
                Place("Rochester", "New York"), Place("St. Louis", "Missouri"), Place("Pittsburgh", "Pennsylvania"),
                Place("Philadelphia", "Pennsylvania"), Place("Denver", "Colorado"), Place("Dallas", "Texas"),
                Place("Anchorage", "Alaska"), Place("Madison", "Wisconsin"), Place("Fargo", "North Dakota"),
                Place("Los Angeles", "California"), Place("Scottsdale", "Arizona"),
            ]),
        new(
            Country.Sweden,
            Weight: 8,
            FirstNames:
            [
                "Erik", "Oskar", "Viktor", "Gustav", "Filip", "Elias", "Linus", "Rasmus",
                "Jesper", "Anton", "Mattias", "Henrik", "Nils", "Albin",
            ],
            LastNames:
            [
                "Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson", "Lindqvist", "Berglund",
                "Lindström", "Ekholm", "Holmberg", "Sandberg", "Hedman", "Forsberg",
            ],
            Birthplaces:
            [
                Place("Stockholm"), Place("Gothenburg"), Place("Malmö"), Place("Örnsköldsvik"),
                Place("Luleå"), Place("Skellefteå"), Place("Umeå"), Place("Västerås"),
                Place("Jönköping"), Place("Linköping"), Place("Karlstad"), Place("Sundsvall"),
            ]),
        new(
            Country.Finland,
            Weight: 5,
            FirstNames:
            [
                "Mikko", "Aleksander", "Sebastian", "Kasperi", "Eetu", "Joonas", "Teuvo", "Ville",
                "Juuso", "Lauri", "Patrik", "Henri", "Otto", "Niko",
            ],
            LastNames:
            [
                "Virtanen", "Korhonen", "Mäkinen", "Nieminen", "Laine", "Heikkinen", "Koskinen", "Järvinen",
                "Lehtonen", "Salonen", "Granlund", "Hakala", "Lindholm", "Aho",
            ],
            Birthplaces:
            [
                Place("Helsinki"), Place("Espoo"), Place("Tampere"), Place("Turku"),
                Place("Oulu"), Place("Jyväskylä"), Place("Lahti"), Place("Pori"),
                Place("Kuopio"), Place("Rauma"), Place("Hämeenlinna"), Place("Vantaa"),
            ]),
        new(
            Country.Russia,
            Weight: 5,
            FirstNames:
            [
                "Alexander", "Dmitri", "Nikita", "Sergei", "Artemi", "Evgeni", "Kirill", "Ilya",
                "Vladislav", "Andrei", "Pavel", "Ivan", "Maxim", "Yegor",
            ],
            LastNames:
            [
                "Ivanov", "Petrov", "Smirnov", "Kuznetsov", "Popov", "Volkov", "Sokolov", "Morozov",
                "Orlov", "Fedorov", "Zaitsev", "Belov", "Gusev", "Romanov",
            ],
            Birthplaces:
            [
                Place("Moscow"), Place("Saint Petersburg"), Place("Yaroslavl"), Place("Chelyabinsk"),
                Place("Magnitogorsk"), Place("Omsk"), Place("Kazan"), Place("Ufa"),
                Place("Novosibirsk"), Place("Cherepovets"), Place("Nizhny Novgorod"), Place("Togliatti"),
            ]),
        new(
            Country.Czechia,
            Weight: 4,
            FirstNames:
            [
                "Jakub", "Tomáš", "Ondřej", "Martin", "David", "Lukáš", "Filip", "Michal",
                "Radek", "Jan", "Vojtěch", "Pavel",
            ],
            LastNames:
            [
                "Novák", "Svoboda", "Dvořák", "Černý", "Procházka", "Kučera", "Veselý", "Horák",
                "Němec", "Pokorný", "Hájek", "Krejčí",
            ],
            Birthplaces:
            [
                Place("Prague"), Place("Brno"), Place("Ostrava"), Place("Pardubice"),
                Place("Plzeň"), Place("Kladno"), Place("Liberec"), Place("Zlín"),
                Place("Třinec"), Place("Litvínov"), Place("České Budějovice"), Place("Olomouc"),
            ]),
        new(
            Country.Switzerland,
            Weight: 2,
            FirstNames:
            [
                "Nico", "Timo", "Roman", "Luca", "Jonas", "Dario", "Kevin", "Yannick",
                "Fabian", "Janis", "Pius", "Simon",
            ],
            LastNames:
            [
                "Müller", "Meier", "Schmid", "Keller", "Weber", "Fischer", "Huber", "Brunner",
                "Baumann", "Frei", "Moser", "Gerber",
            ],
            Birthplaces:
            [
                Place("Zurich"), Place("Bern"), Place("Geneva"), Place("Lugano"), Place("Davos"),
                Place("Lausanne"), Place("Biel"), Place("Fribourg"), Place("Zug"), Place("Langnau"),
            ]),
        new(
            Country.Germany,
            Weight: 2,
            FirstNames:
            [
                "Leon", "Tim", "Moritz", "Lukas", "Tobias", "Dominik", "Felix", "Maximilian",
                "Jonas", "Marco", "Philipp", "Nico",
            ],
            LastNames:
            [
                "Schmidt", "Schneider", "Wagner", "Becker", "Hoffmann", "Schäfer", "Koch", "Richter",
                "Wolf", "Krüger", "Zimmermann", "Braun",
            ],
            Birthplaces:
            [
                Place("Berlin"), Place("Munich"), Place("Cologne"), Place("Mannheim"),
                Place("Düsseldorf"), Place("Hamburg"), Place("Füssen"), Place("Rosenheim"),
                Place("Landshut"), Place("Bad Tölz"), Place("Augsburg"), Place("Krefeld"),
            ]),
        new(
            Country.Slovakia,
            Weight: 2,
            FirstNames:
            [
                "Marek", "Tomáš", "Juraj", "Peter", "Martin", "Michal", "Šimon", "Adam",
                "Matej", "Richard", "Erik", "Samuel",
            ],
            LastNames:
            [
                "Horváth", "Kováč", "Varga", "Tóth", "Nagy", "Baláž", "Szabó", "Molnár",
                "Lukáč", "Kollár", "Hudák", "Gašpar",
            ],
            Birthplaces:
            [
                Place("Bratislava"), Place("Košice"), Place("Nitra"), Place("Trenčín"), Place("Žilina"),
                Place("Zvolen"), Place("Poprad"), Place("Banská Bystrica"), Place("Martin"), Place("Michalovce"),
            ]),
        new(
            Country.Denmark,
            Weight: 1,
            FirstNames:
            [
                "Mads", "Frederik", "Oliver", "Nikolaj", "Mikkel", "Rasmus", "Jonas", "Lars",
                "Kasper", "Morten", "Anders", "Emil",
            ],
            LastNames:
            [
                "Jensen", "Nielsen", "Hansen", "Pedersen", "Andersen", "Christensen", "Larsen", "Sørensen",
                "Rasmussen", "Poulsen", "Madsen", "Kristensen",
            ],
            Birthplaces:
            [
                Place("Copenhagen"), Place("Aarhus"), Place("Herning"), Place("Odense"),
                Place("Aalborg"), Place("Esbjerg"), Place("Rødovre"), Place("Frederikshavn"),
            ]),
        new(
            Country.Latvia,
            Weight: 1,
            FirstNames:
            [
                "Kristaps", "Rūdolfs", "Artūrs", "Jānis", "Kārlis", "Roberts", "Elvis", "Uvis",
                "Oskars", "Teodors", "Ronalds", "Edgars",
            ],
            LastNames:
            [
                "Bērziņš", "Kalniņš", "Ozols", "Liepiņš", "Krūmiņš", "Balodis", "Zariņš", "Vītols",
                "Jansons", "Ozoliņš", "Siliņš", "Freibergs",
            ],
            Birthplaces:
            [
                Place("Riga"), Place("Liepāja"), Place("Jelgava"), Place("Daugavpils"), Place("Ogre"), Place("Ventspils"),
            ]),
        new(
            Country.Austria,
            Weight: 1,
            FirstNames:
            [
                "Thomas", "Marco", "Michael", "Stefan", "Florian", "Manuel", "Raphael", "Benjamin",
                "Dominic", "Lukas", "Konstantin", "Paul",
            ],
            LastNames:
            [
                "Gruber", "Huber", "Bauer", "Wagner", "Pichler", "Steiner", "Moser", "Mayer",
                "Hofer", "Leitner", "Berger", "Fuchs",
            ],
            Birthplaces:
            [
                Place("Vienna"), Place("Graz"), Place("Salzburg"), Place("Innsbruck"),
                Place("Klagenfurt"), Place("Villach"), Place("Linz"), Place("Feldkirch"),
            ]),
        new(
            Country.Norway,
            Weight: 1,
            FirstNames:
            [
                "Mats", "Andreas", "Ole", "Sondre", "Henrik", "Magnus", "Tobias", "Kristian",
                "Stian", "Eirik", "Petter", "Jonas",
            ],
            LastNames:
            [
                "Hansen", "Johansen", "Olsen", "Larsen", "Andersen", "Berg", "Haugen", "Bakke",
                "Dahl", "Lie", "Holm", "Strand",
            ],
            Birthplaces:
            [
                Place("Oslo"), Place("Stavanger"), Place("Lillehammer"), Place("Hamar"),
                Place("Trondheim"), Place("Bergen"), Place("Fredrikstad"), Place("Lørenskog"),
            ]),
    ];

    public static CountryOrigin For(Country country) => Countries.Single(origin => origin.Country == country);

    private static PlaceDefinition Place(string city, string? region = null) => new(city, region);

    internal sealed record CountryOrigin(
        Country Country,
        int Weight,
        IReadOnlyList<string> FirstNames,
        IReadOnlyList<string> LastNames,
        IReadOnlyList<PlaceDefinition> Birthplaces);

    internal sealed record PlaceDefinition(string City, string? Region);
}