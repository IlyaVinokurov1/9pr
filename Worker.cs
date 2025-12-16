using System.Net.NetworkInformation;
using System.Threading.Tasks;
using TaskManagerTelegramBot_Vinokurov.Classes;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TaskManagerTelegramBot_Vinokurov
{
    public class Worker : BackgroundService
    {
        readonly string Token = "8549300089:AAFQyDSrINxsDFVl60atz8c-E4nZuyVbUCY";
        TelegramBotClient TelegramBotClient;
        DbContext dbContext;
        Timer Timer;

        List<string> Messages = new List<string>()
        {
            // 0 - Приветствие
            "Здравствуйте! " +
            "\nРады приветствовать вас в Telegram-боте «Напоминатор»!" +
            "\nНаш бот создан для того, чтобы напоминать вам о важных событиях и мероприятиях. " +
            "С ним вы точно не пропустите ничего важного!" +
            "\nНе забудьте добавить бота в список своих контактов и настроить уведомления. " +
            "Тогда вы всегда будете в курсе событий!",

            // 1 - Формат задачи
            "Формат обычной задачи:" +
            "\n<b>12:51 26.01.2025</b>" +
            "\nКупить продукты" +
            "\n\nФормат повторяющейся задачи:" +
            "\n<b>21:00 каждый ПН,СР,ПТ</b>" +
            "\nВыпить таблетки" +
            "\n\nДни недели: ПН, ВТ, СР, ЧТ, ПТ, СБ, ВС",

            // 2 - Успех
            "Задача создана!",

            // 3 - Ошибка
            "Ошибка. Проверьте формат.",

            // 4 - Дата в прошлом
            "Это время уже прошло.",

            // 5 - Нет задач
            "Задач нет.",

            // 6 - Задача удалена
            "Задача удалена.",

            // 7 - Все задачи удалены
            "Все задачи удалены."
        };

        // Словарь дней недели
        private Dictionary<string, DayOfWeek> dayMap = new Dictionary<string, DayOfWeek>
        {
            {"ПН", DayOfWeek.Monday},
            {"ВТ", DayOfWeek.Tuesday},
            {"СР", DayOfWeek.Wednesday},
            {"ЧТ", DayOfWeek.Thursday},
            {"ПТ", DayOfWeek.Friday},
            {"СБ", DayOfWeek.Saturday},
            {"ВС", DayOfWeek.Sunday}
        };

        public bool CheckFormatDateTime(string value, out DateTime time)
        {
            return DateTime.TryParse(value, out time);
        }

        public static ReplyKeyboardMarkup GetButtons()
        {
            List<KeyboardButton> keyboardButtons = new List<KeyboardButton>();
            keyboardButtons.Add(new KeyboardButton("Удалить все задачи"));
            return new ReplyKeyboardMarkup
            {
                Keyboard = new List<List<KeyboardButton>>() { keyboardButtons }
            };
        }

        public static InlineKeyboardMarkup DeleteEvent(int eventId)
        {
            List<InlineKeyboardButton> inlineKeyboards = new List<InlineKeyboardButton>();
            inlineKeyboards.Add(new InlineKeyboardButton("Удалить")
            {
                CallbackData = eventId.ToString()
            });
            return new InlineKeyboardMarkup(inlineKeyboards);
        }

        public async void SendMessage(long chatId, int typeMessage)
        {
            if (typeMessage != 4) 
            {
                await TelegramBotClient.SendMessage(
                    chatId,
                    Messages[typeMessage],
                    ParseMode.Html,
                    replyMarkup: GetButtons()
                    );
            }
            else
            {
                await TelegramBotClient.SendMessage(
                    chatId,
                    Messages[typeMessage] + $" Сейчас: {DateTime.Now.ToString("HH:mm dd.MM.yyyy")}");
            }
        }

        public async void Command(long chatId, string command)
        {
            if (command.ToLower() == "/start") SendMessage(chatId, 0);
            else if (command.ToLower() == "/create_task") SendMessage(chatId, 1);
            else if (command.ToLower() == "/list_tasks")
            {
                Users User = dbContext.GetUser(chatId);

                if (User == null || User.Events.Count == 0)
                {
                    SendMessage(chatId, 5);
                }
                else
                {
                    foreach (Events Event in User.Events)
                    {
                        string repeatInfo = string.IsNullOrEmpty(Event.RepeatPattern)
                            ? "Однократно"
                            : $"Повторяется: {Event.RepeatPattern}";
                        string timeInfo = string.IsNullOrEmpty(Event.RepeatPattern)
                            ? $"Время: {Event.Time.ToString("HH:mm dd.MM.yyyy")}"
                            : $"Время: {Event.Time.ToString("HH:mm")}";

                        await TelegramBotClient.SendMessage(
                            chatId,
                            $"{timeInfo}" +
                            $"\n{repeatInfo}" +
                            $"\nСообщение: {Event.Message}",
                            replyMarkup: DeleteEvent(Event.Id)
                            );
                    }
                }
            }
        }

        private void GetMessages(Message message)
        {
            Console.WriteLine("Получено сообщение: " + message.Text + " от пользователя: " + message.Chat.Username);

            if (message.Text.Contains("/"))
            {
                Command(message.Chat.Id, message.Text);
                return;
            }

            if (message.Text.Equals("Удалить все задачи"))
            {
                dbContext.DeleteAllUserEvents(message.Chat.Id);
                SendMessage(message.Chat.Id, 7);
                return;
            }

            Users User = dbContext.GetUser(message.Chat.Id);

            string[] lines = message.Text.Split('\n');
            if (lines.Length < 2)
            {
                SendMessage(message.Chat.Id, 3);
                return;
            }

            string firstLine = lines[0].Trim();
            string taskMessage = lines[1].Trim();

            DateTime Time;
            string repeatPattern = null;

            // Определяем тип задачи
            if (firstLine.Contains("каждый"))
            {
                // Повторяющаяся задача: "21:00 каждый ПН,СР,ПТ"
                string[] parts = firstLine.Split(' ');

                if (parts.Length < 3 || !DateTime.TryParse(parts[0], out Time))
                {
                    SendMessage(message.Chat.Id, 3);
                    return;
                }

                repeatPattern = parts[2]; // Дни недели

                // Устанавливаем на сегодня с указанным временем
                Time = DateTime.Today.Add(Time.TimeOfDay);

                // Если время уже прошло, добавляем 1 день
                if (Time < DateTime.Now)
                {
                    Time = Time.AddDays(1);
                }
            }
            else
            {
                // Обычная задача: "12:51 26.01.2025"
                if (!DateTime.TryParse(firstLine, out Time))
                {
                    SendMessage(message.Chat.Id, 3);
                    return;
                }

                if (Time < DateTime.Now)
                {
                    SendMessage(message.Chat.Id, 4);
                    return;
                }
            }

            // Создаем и сохраняем
            var newEvent = new Events(Time, taskMessage, repeatPattern);
            int eventId = dbContext.AddEvent(User.IdUser, newEvent);
            newEvent.Id = eventId;

            if (User.Events == null)
                User.Events = new List<Events>();

            User.Events.Add(newEvent);

            SendMessage(message.Chat.Id, 2);
        }

        private async Task HandleUpdateAsync(
            ITelegramBotClient client,
            Update update,
            CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message)
                GetMessages(update.Message);
            else if (update.Type == UpdateType.CallbackQuery)
            {
                CallbackQuery query = update.CallbackQuery;

                if (int.TryParse(query.Data, out int eventId))
                {
                    dbContext.DeleteEvent(eventId, query.Message.Chat.Id);
                    SendMessage(query.Message.Chat.Id, 6);
                }
            }
        }

        private async Task HandleErrorAsync(
            ITelegramBotClient client,
            Exception exception,
            HandleErrorSource source,
            CancellationToken token)
        {
            Console.WriteLine("Ошибка: " + exception.Message);
        }

        // Метод для расчета следующего выполнения по дням недели
        private DateTime GetNextDay(DateTime currentTime, string repeatPattern)
        {
            if (string.IsNullOrEmpty(repeatPattern))
                return currentTime;

            // Получаем дни недели
            var days = new List<DayOfWeek>();
            var dayCodes = repeatPattern.Split(',');

            foreach (var dayCode in dayCodes)
            {
                var code = dayCode.Trim().ToUpper();
                if (dayMap.ContainsKey(code))
                {
                    days.Add(dayMap[code]);
                }
            }

            if (days.Count == 0)
                return currentTime;

            // Ищем следующий подходящий день
            DateTime nextDate = currentTime;

            for (int i = 1; i <= 7; i++)
            {
                nextDate = currentTime.AddDays(i);
                if (days.Contains(nextDate.DayOfWeek))
                {
                    return nextDate;
                }
            }

            return currentTime;
        }

        public async void Tick(object obj)
        {
            var activeEvents = dbContext.GetActiveEvents();

            foreach (var (userId, eventItem) in activeEvents)
            {
                // Отправляем напоминание
                await TelegramBotClient.SendMessage(userId, "Напоминание: " + eventItem.Message);

                if (!string.IsNullOrEmpty(eventItem.RepeatPattern))
                {
                    // Обновляем на следующий день по расписанию
                    DateTime nextTime = GetNextDay(eventItem.Time, eventItem.RepeatPattern);
                    dbContext.UpdateEventTime(eventItem.Id, nextTime);
                }
                else
                {
                    // Удаляем однократную задачу
                    dbContext.DeleteEvent(eventItem.Id, userId);
                }
            }
        }

        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            dbContext = new DbContext();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TelegramBotClient = new TelegramBotClient(Token);

            TelegramBotClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                null,
                new CancellationTokenSource().Token);

            TimerCallback TimerCallback = new TimerCallback(Tick);
            Timer = new Timer(TimerCallback, 0, 0, 60 * 1000);
        }
    }
}