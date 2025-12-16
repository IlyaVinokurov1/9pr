using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace TaskManagerTelegramBot_Vinokurov.Classes
{
    public class DbContext
    {
        private string _connectionString = "Server=localhost;port=3307;Database=TaskManagerDB;User=root;Password=;";

        public DbContext()
        {
            // Если нужно изменить строку подключения, можно добавить метод SetConnectionString
        }

        // Получить пользователя из БД (всегда возвращает пользователя, создает если не существует)
        public Users GetUser(long userId)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                // Проверяем, существует ли пользователь
                var checkUserCmd = new MySqlCommand("SELECT IdUser FROM Users WHERE IdUser = @IdUser", connection);
                checkUserCmd.Parameters.AddWithValue("@IdUser", userId);

                var userExists = checkUserCmd.ExecuteScalar() != null;

                if (!userExists)
                {
                    // Создаем нового пользователя
                    var createUserCmd = new MySqlCommand("INSERT INTO Users (IdUser) VALUES (@IdUser)", connection);
                    createUserCmd.Parameters.AddWithValue("@IdUser", userId);
                    createUserCmd.ExecuteNonQuery();
                }

                // Создаем объект пользователя
                var user = new Users
                {
                    IdUser = userId,
                    Events = new List<Events>()
                };

                // Загружаем события пользователя
                var getEventsCmd = new MySqlCommand("SELECT Id, Message, EventTime FROM Events WHERE IdUser = @IdUser", connection);
                getEventsCmd.Parameters.AddWithValue("@IdUser", userId);

                using var reader = getEventsCmd.ExecuteReader();
                while (reader.Read())
                {
                    user.Events.Add(new Events
                    {
                        Id = reader.GetInt32("Id"),
                        Message = reader.GetString("Message"),
                        Time = reader.GetDateTime("EventTime")
                    });
                }

                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении пользователя: {ex.Message}");
                // В случае ошибки возвращаем нового пользователя
                return new Users
                {
                    IdUser = userId,
                    Events = new List<Events>()
                };
            }
        }

        // Добавить событие
        public void AddEvent(long userId, Events eventItem)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                var cmd = new MySqlCommand(
                    "INSERT INTO Events (IdUser, Message, EventTime) VALUES (@IdUser, @Message, @EventTime)",
                    connection);

                cmd.Parameters.AddWithValue("@IdUser", userId);
                cmd.Parameters.AddWithValue("@Message", eventItem.Message);
                cmd.Parameters.AddWithValue("@EventTime", eventItem.Time);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при добавлении события: {ex.Message}");
            }
        }

        // Удалить событие
        public void DeleteEvent(string message, long userId)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                var cmd = new MySqlCommand("DELETE FROM Events WHERE Message = @Message AND IdUser = @IdUser", connection);
                cmd.Parameters.AddWithValue("@Message", message);
                cmd.Parameters.AddWithValue("@IdUser", userId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при удалении события: {ex.Message}");
            }
        }

        // Удалить все события пользователя
        public void DeleteAllUserEvents(long userId)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                var cmd = new MySqlCommand("DELETE FROM Events WHERE IdUser = @IdUser", connection);
                cmd.Parameters.AddWithValue("@IdUser", userId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при удалении всех событий: {ex.Message}");
            }
        }

        // Получить все активные события (для таймера)
        public List<(long userId, Events eventItem)> GetActiveEvents()
        {
            var result = new List<(long, Events)>();

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                var currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                var cmd = new MySqlCommand(
                    "SELECT Id, IdUser, Message, EventTime FROM Events WHERE DATE_FORMAT(EventTime, '%Y-%m-%d %H:%i') = @CurrentTime",
                    connection);

                cmd.Parameters.AddWithValue("@CurrentTime", currentTime);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var eventItem = new Events
                    {
                        Id = reader.GetInt32("Id"),
                        Message = reader.GetString("Message"),
                        Time = reader.GetDateTime("EventTime")
                    };

                    result.Add((reader.GetInt64("IdUser"), eventItem));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении активных событий: {ex.Message}");
            }

            return result;
        }
    }
}