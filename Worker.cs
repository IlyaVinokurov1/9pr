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
        DbContext dbContext; // Добавляем DbContext
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

        public async void Command(long chatId, string command)
        {
            if (command.ToLower() == "/start") SendMessage(chatId, 0);
            else if (command.ToLower() == "/create_task") SendMessage(chatId, 1);
            else if (command.ToLower() == "/list_tasks")
            {
                // Вместо поиска в локальном списке - получаем из БД
                Users User = dbContext.GetUser(chatId);

                if (User == null || User.Events.Count == 0)
                {
                    SendMessage(chatId, 4);
                }
                else
                {
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

        private void GetMessages(Message message)
        {
            Console.WriteLine("Получено сообщение: " + message.Text + " от пользователя: " + message.Chat.Username);
            long IdUser = message.Chat.Id;
            string MessageUser = message.Text;

            if (message.Text.Contains("/"))
                Command(message.Chat.Id, message.Text);
            else if (message.Text.Equals("Удалить все задачи"))
            {
                dbContext.DeleteAllUserEvents(message.Chat.Id);
                SendMessage(message.Chat.Id, 6);
            }
            else
            {
                Users User = dbContext.GetUser(message.Chat.Id);

                string[] Info = message.Text.Split('\n');
                if (Info.Length < 2)
                {
                    SendMessage(message.Chat.Id, 2);
                    return;
                }

                DateTime Time;
                if (CheckFormatDateTime(Info[0], out Time) == false)
                {
                    SendMessage(message.Chat.Id, 2);
                    return;
                }

                if (Time < DateTime.Now)
                {
                    SendMessage(message.Chat.Id, 3);
                    return;
                }
                string eventMessage = message.Text.Replace(Time.ToString("HH:mm dd.MM.yyyy") + "\n", "");
                var newEvent = new Events(Time, eventMessage);
                dbContext.AddEvent(User.IdUser, newEvent);
                User.Events.Add(newEvent);

                SendMessage(message.Chat.Id, 1);
            }
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
                // Удаляем из БД по сообщению
                dbContext.DeleteEvent(query.Data, query.Message.Chat.Id);
                SendMessage(query.Message.Chat.Id, 5);
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

        public async void Tick(object obj)
        {
            string TimeNow = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            // Получаем активные события из БД
            var activeEvents = dbContext.GetActiveEvents();

            foreach (var (userId, eventItem) in activeEvents)
            {
                await TelegramBotClient.SendMessage(
                    userId,
                    "Напоминание: " + eventItem.Message);

                // Удаляем событие из БД после отправки
                dbContext.DeleteEvent(eventItem.Message, userId);
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