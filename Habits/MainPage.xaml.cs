using System.Collections.ObjectModel;
using System.Globalization;

namespace Habits
{
    public partial class MainPage : ContentPage
    {
        ObservableCollection<Habit> habits = new();
        Habit selectedHabit = null;
        bool isEditing = false;
        const string HabitCsvPath = "habits.csv";
        const string HabitLogCsvPath = "habit_log.csv";

        public MainPage()
        {
            InitializeComponent();
            LoadHabits();
            ListPage.ItemsSource = habits;
        }

        void LoadHabits()
        {
            habits.Clear();
            if (!File.Exists(HabitCsvPath)) return;

            foreach (var line in File.ReadAllLines(HabitCsvPath))
            {
                var parts = line.Split(';');
                if (parts.Length >= 4)
                {
                    habits.Add(new Habit
                    {
                        Id = parts[0],
                        Created = DateTime.Parse(parts[1]),
                        Name = parts[2],
                        Description = parts[3],
                        ShortDescription = parts[3].Length > 50 ? parts[3][..50] + "..." : parts[3]
                    });
                }
            }
        }

        void SaveHabits()
        {
            File.WriteAllLines(HabitCsvPath, habits.Select(h =>
                $"{h.Id};{h.Created};{h.Name};{h.Description}"));
        }

        void ShowPage(string page)
        {
            ListPage.IsVisible = page == "list";
            DetailPage.IsVisible = page == "detail";
            EditPage.IsVisible = page == "edit";

            PageTitle.Text = page switch
            {
                "list" => "Список привычек",
                "detail" => "Подробнее",
                "edit" => "Редактирование",
                _ => ""
            };
        }

        void OnBackClicked(object sender, EventArgs e)
        {
            selectedHabit = null;
            isEditing = false;
            ShowPage("list");
        }

        void OnAddClicked(object sender, EventArgs e)
        {
            if (EditPage.IsVisible)
            {
                DisplayAlert("Ошибка", "Редактирование уже открыто", "OK");
                return;
            }

            selectedHabit = null;
            isEditing = true;

            EditName.Text = "";
            EditDescription.Text = "";
            TargetCountEntry.Text = "";
            TargetPeriodEntry.Text = "";
            TargetUnitPicker.SelectedIndex = 0;

            ShowPage("edit");
        }

        void OnDetailClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is Habit habit)
            {
                selectedHabit = habit;
                DetailName.Text = habit.Name;
                DetailDescription.Text = habit.Description;
                LoadCompletions(habit.Id);
                ShowPage("detail");
            }
        }

        void OnEditClicked(object sender, EventArgs e)
        {
            if (selectedHabit == null) return;

            isEditing = true;
            EditName.Text = selectedHabit.Name;
            EditDescription.Text = selectedHabit.Description;
            ShowPage("edit");
        }

        async void OnDeleteHabitClicked(object sender, EventArgs e)
        {
            if (selectedHabit == null) return;

            bool confirm = await DisplayAlert("Удаление", $"Удалить привычку «{selectedHabit.Name}»?", "Да", "Нет");
            if (confirm)
            {
                habits.Remove(selectedHabit);
                SaveHabits();

                if (File.Exists(HabitLogCsvPath))
                {
                    var lines = File.ReadAllLines(HabitLogCsvPath)
                        .Where(line => !line.EndsWith($";{selectedHabit.Id}"))
                        .ToArray();
                    File.WriteAllLines(HabitLogCsvPath, lines);
                }

                selectedHabit = null;
                ShowPage("list");
            }
        }

        void OnSaveClicked(object sender, EventArgs e)
        {
            string name = EditName.Text?.Trim();
            string desc = EditDescription.Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                DisplayAlert("Ошибка", "Название обязательно", "OK");
                return;
            }

            if (selectedHabit != null)
            {
                selectedHabit.Name = name;
                selectedHabit.Description = desc;
                selectedHabit.ShortDescription = desc.Length > 50 ? desc.Substring(0, 50) + "..." : desc;
            }
            else
            {
                var newHabit = new Habit
                {
                    Id = Guid.NewGuid().ToString(),
                    Created = DateTime.Now,
                    Name = name,
                    Description = desc,
                    ShortDescription = desc.Length > 50 ? desc.Substring(0, 50) + "..." : desc
                };
                habits.Add(newHabit);
            }

            SaveHabits();
            isEditing = false;
            ShowPage("list");
        }

        async void OnCompleteClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is Habit habit)
            {
                bool confirmed = await DisplayAlert("Подтвердите", $"Вы выполнили «{habit.Name}»?", "Да", "Нет");
                if (confirmed)
                {
                    string logLine = $"{DateTime.Now};{habit.Id}";
                    File.AppendAllLines(HabitLogCsvPath, new[] { logLine });
                    await DisplayAlert("Отлично", "Привычка записана!", "ОК");
                }
            }
        }

        void LoadCompletions(string habitId)
        {
            if (!File.Exists(HabitLogCsvPath)) return;

            var completions = File.ReadAllLines(HabitLogCsvPath)
                .Select(line => line.Split(';'))
                .Where(parts => parts.Length == 2 && parts[1] == habitId)
                .Select(parts => DateTime.Parse(parts[0]).ToString("g"))
                .ToList();

            CompletionList.ItemsSource = completions;
        }

        async void OnDeleteCompletionClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is string dateString &&
                selectedHabit != null &&
                DateTime.TryParse(dateString, out var date))
            {
                bool confirmed = await DisplayAlert("Удалить запись", $"Удалить выполнение за {date:g}?", "Да", "Нет");
                if (confirmed)
                {
                    var lines = File.ReadAllLines(HabitLogCsvPath)
                        .Where(line => line != $"{date};{selectedHabit.Id}")
                        .ToList();

                    File.WriteAllLines(HabitLogCsvPath, lines);
                    LoadCompletions(selectedHabit.Id);
                }
            }
        }

        async void OnExportStatsClicked(object sender, EventArgs e)
        {
            if (selectedHabit == null) return;
            if (!File.Exists(HabitLogCsvPath)) return;

            var logEntries = File.ReadAllLines(HabitLogCsvPath)
                .Select(line => line.Split(';'))
                .Where(parts => parts.Length == 2 && parts[1] == selectedHabit.Id)
                .Select(parts => DateTime.Parse(parts[0]))
                .ToList();

            if (!logEntries.Any())
            {
                await DisplayAlert("Нет данных", "Для этой привычки пока нет статистики.", "ОК");
                return;
            }

            var minDate = logEntries.Min().Date;
            var maxDate = logEntries.Max().Date;

            var allDates = Enumerable.Range(0, (maxDate - minDate).Days + 1)
                .Select(offset => minDate.AddDays(offset))
                .ToList();

            var hourRange = Enumerable.Range(0, 24).ToList();

            var matrix = hourRange
                .Select(hour =>
                    allDates.Select(date =>
                        logEntries.Count(dt => dt.Date == date && dt.Hour == hour)
                    ).ToArray())
                .ToArray();

            var header = "," + string.Join(",", allDates.Select(d => d.ToString("yyyy-MM-dd")));
            var rows = hourRange.Select((h, i) =>
                $"{h}:00-{(h + 1) % 24}:00," + string.Join(",", matrix[i])
            );

            var exportLines = new List<string> { header };
            exportLines.AddRange(rows);

            // Имя файла на основе названия привычки
            string safeName = string.Join("_", selectedHabit.Name.Split(Path.GetInvalidFileNameChars()));
            string fileName = $"habit_stats_{safeName}.csv";

            // Путь к "Документам"
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string fullPath = Path.Combine(documentsPath, fileName);

            File.WriteAllLines(fullPath, exportLines);

            await DisplayAlert("Экспорт завершён", $"Файл сохранён в:\n{fullPath}", "ОК");
        }
    }

    public class Habit
    {
        public string Id { get; set; }
        public DateTime Created { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ShortDescription { get; set; }
    }

}
