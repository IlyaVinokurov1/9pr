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
            "Здравствуйте! " +
            "\nРады приветствовать вас в Telegram-боте «Напоминатор»!" +
            "\nНаш бот создан для того, чтобы напоминать вам о важных событиях и мероприятиях. " +
            "С ним вы точно не пропустите ничего важного!" +
            "\nНе забудьте добавить бота в список своих контактов и настроить уведомления. " +
            "Тогда вы всегда будете в курсе событий!",

            "Укажите дату и время напоминания в следующем формате:" +
            "\n<i><b>12:51 26.01.2025</b>" +
            "\nНапомни о том что я хотел сходить в магазин.</i>",

            "Кажется, что-то не получилось." +
            "Укажите дату и время напоминания в следующем формате:" +
            "\n<i><b>12:51 26.01.2025</b>" +
            "\nНапомни о том что я хотел сходить в магазин.</i>",
            "",
            "Задачи пользователя не найдены.",
            "Событие удалено.",
            "Все события удалены."
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

        public static InlineKeyboardMarkup DeleteEvent(string Message)
        {
            List<InlineKeyboardButton> inlineKeyboards = new List<InlineKeyboardButton>();
            inlineKeyboards.Add(new InlineKeyboardButton("Удалить", Message));
            return new InlineKeyboardMarkup(inlineKeyboards);
        }

        public async void SendMessage(long chatId, int typeMessage)
        {
            try
            {
                if (typeMessage != 3)
                {
                    await TelegramBotClient.SendMessage(
                        chatId,
                        Messages[typeMessage],
                        ParseMode.Html,
                        replyMarkup: GetButtons()
                        );
                }
                else if (typeMessage == 3)
                {
                    await TelegramBotClient.SendMessage(
                        chatId,
                        $"Указанное вами время и дата не могут быть установлены, " +
                        $"потому что сейчас уже: {DateTime.Now.ToString("HH:mm dd.MM.yyyy")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки сообщения: {ex.Message}");
            }
        }

        public async void Command(long chatId, string command)
        {
            try
            {
                if (command.ToLower() == "/start") SendMessage(chatId, 0);
                else if (command.ToLower() == "/create_task") SendMessage(chatId, 1);
                else if (command.ToLower() == "/list_tasks")
                {
                    Users User = dbContext.GetUser(chatId);

                    if (User == null)
                    {
                        Console.WriteLine("Пользователь не найден");
                        SendMessage(chatId, 4);
                    }
                    else if (User.Events == null || User.Events.Count == 0)
                    {
                        Console.WriteLine($"Событий у пользователя {chatId} нет");
                        SendMessage(chatId, 4);
                    }
                    else
                    {
                        Console.WriteLine($"Найдено {User.Events.Count} событий у пользователя {chatId}");
                        foreach (Events Event in User.Events)
                        {
                            await TelegramBotClient.SendMessage(
                                chatId,
                                $"Уведомить пользователя: {Event.Time.ToString("HH:mm dd.MM.yyyy")}" +
                                $"\n Сообщение: {Event.Message}",
                                replyMarkup: DeleteEvent(Event.Message)
                                );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в Command: {ex.Message}");
            }
        }

        private void GetMessages(Message message)
        {
            try
            {
                Console.WriteLine("Получено сообщение: " + message.Text + " от пользователя: " + message.Chat.Username);

                if (message.Text == null)
                {
                    Console.WriteLine("Текст сообщения null");
                    return;
                }

                if (message.Text.Contains("/"))
                {
                    Command(message.Chat.Id, message.Text);
                    return;
                }

                if (message.Text.Equals("Удалить все задачи"))
                {
                    dbContext.DeleteAllUserEvents(message.Chat.Id);
                    SendMessage(message.Chat.Id, 6);
                    return;
                }

                // Получаем пользователя из БД
                Console.WriteLine($"Получаем пользователя {message.Chat.Id} из БД...");
                Users User = dbContext.GetUser(message.Chat.Id);

                if (User == null)
                {
                    Console.WriteLine($"Ошибка: пользователь {message.Chat.Id} не найден");
                    SendMessage(message.Chat.Id, 2);
                    return;
                }

                Console.WriteLine($"Пользователь {message.Chat.Id} получен, количество событий: {User.Events?.Count ?? 0}");

                string[] Info = message.Text.Split('\n');
                if (Info.Length < 2)
                {
                    Console.WriteLine("Неправильный формат: меньше 2 строк");
                    SendMessage(message.Chat.Id, 2);
                    return;
                }

                DateTime Time;
                string firstLine = Info[0].Trim();
                Console.WriteLine($"Пытаемся распарсить дату: '{firstLine}'");

                if (CheckFormatDateTime(firstLine, out Time) == false)
                {
                    Console.WriteLine($"Не удалось распарсить дату: '{firstLine}'");
                    SendMessage(message.Chat.Id, 2);
                    return;
                }

                Console.WriteLine($"Распарсена дата: {Time}");

                if (Time < DateTime.Now)
                {
                    Console.WriteLine($"Дата в прошлом: {Time} < {DateTime.Now}");
                    SendMessage(message.Chat.Id, 3);
                    return;
                }

                string timeString = Time.ToString("HH:mm dd.MM.yyyy");
                string eventMessage = message.Text.Replace(timeString + "\n", "").Replace(timeString, "").Trim();

                Console.WriteLine($"Создаем событие: время={Time}, сообщение='{eventMessage}'");

                var newEvent = new Events(Time, eventMessage);

                // Сохраняем в БД
                Console.WriteLine("Сохраняем в БД...");
                dbContext.AddEvent(User.IdUser, newEvent);

                // Добавляем в локальный список
                if (User.Events == null)
                {
                    User.Events = new List<Events>();
                }
                User.Events.Add(newEvent);
                Console.WriteLine("Событие добавлено");

                SendMessage(message.Chat.Id, 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в GetMessages: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }

        private async Task HandleUpdateAsync(
            ITelegramBotClient client,
            Update update,
            CancellationToken cancellationToken)
        {
            try
            {
                if (update.Type == UpdateType.Message)
                    GetMessages(update.Message);
                else if (update.Type == UpdateType.CallbackQuery)
                {
                    CallbackQuery query = update.CallbackQuery;
                    Console.WriteLine($"CallbackQuery получен: {query.Data} от {query.Message.Chat.Id}");
                    dbContext.DeleteEvent(query.Data, query.Message.Chat.Id);
                    SendMessage(query.Message.Chat.Id, 5);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HandleUpdateAsync: {ex.Message}");
            }
        }

        private async Task HandleErrorAsync(
            ITelegramBotClient client,
            Exception exception,
            HandleErrorSource source,
            CancellationToken token)
        {
            Console.WriteLine("Ошибка бота: " + exception.Message);
        }

        public async void Tick(object obj)
        {
            try
            {
                Console.WriteLine($"Tick выполняется в {DateTime.Now}");
                var activeEvents = dbContext.GetActiveEvents();
                Console.WriteLine($"Найдено активных событий: {activeEvents.Count}");

                foreach (var (userId, eventItem) in activeEvents)
                {
                    Console.WriteLine($"Отправляем напоминание пользователю {userId}: {eventItem.Message}");
                    await TelegramBotClient.SendMessage(
                        userId,
                        "Напоминание: " + eventItem.Message);

                    dbContext.DeleteEvent(eventItem.Message, userId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в Tick: {ex.Message}");
            }
        }

        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            dbContext = new DbContext();
            Console.WriteLine("DbContext создан");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("Запуск бота...");
            TelegramBotClient = new TelegramBotClient(Token);

            TelegramBotClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                null,
                new CancellationTokenSource().Token);

            TimerCallback TimerCallback = new TimerCallback(Tick);
            Timer = new Timer(TimerCallback, 0, 0, 60 * 1000);
            Console.WriteLine("Таймер запущен");
        }
    }
}