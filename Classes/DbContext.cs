using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace TaskManagerTelegramBot_Vinokurov.Classes
{
    public class DbContext
    {
        private string _connectionString = "Server=localhost;port=3307;Database=TaskManagerDB;User=root;Password=;";

        public Users GetUser(long userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            var checkUserCmd = new MySqlCommand("SELECT IdUser FROM Users WHERE IdUser = @IdUser", connection);
            checkUserCmd.Parameters.AddWithValue("@IdUser", userId);

            var userExists = checkUserCmd.ExecuteScalar() != null;

            if (!userExists)
            {
                var createUserCmd = new MySqlCommand("INSERT INTO Users (IdUser) VALUES (@IdUser)", connection);
                createUserCmd.Parameters.AddWithValue("@IdUser", userId);
                createUserCmd.ExecuteNonQuery();
            }

            var user = new Users
            {
                IdUser = userId,
                Events = new List<Events>()
            };

            var getEventsCmd = new MySqlCommand("SELECT Id, Message, EventTime, RepeatPattern FROM Events WHERE IdUser = @IdUser", connection);
            getEventsCmd.Parameters.AddWithValue("@IdUser", userId);

            using var reader = getEventsCmd.ExecuteReader();
            while (reader.Read())
            {
                user.Events.Add(new Events
                {
                    Id = reader.GetInt32("Id"),
                    Message = reader.GetString("Message"),
                    Time = reader.GetDateTime("EventTime"),
                    RepeatPattern = reader.IsDBNull(reader.GetOrdinal("RepeatPattern")) ? null : reader.GetString("RepeatPattern")
                });
            }

            return user;
        }

        public int AddEvent(long userId, Events eventItem)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            var cmd = new MySqlCommand(
                "INSERT INTO Events (IdUser, Message, EventTime, RepeatPattern) VALUES (@IdUser, @Message, @EventTime, @RepeatPattern); SELECT LAST_INSERT_ID();",
                connection);

            cmd.Parameters.AddWithValue("@IdUser", userId);
            cmd.Parameters.AddWithValue("@Message", eventItem.Message);
            cmd.Parameters.AddWithValue("@EventTime", eventItem.Time);
            cmd.Parameters.AddWithValue("@RepeatPattern", (object)eventItem.RepeatPattern ?? DBNull.Value);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void DeleteEvent(int eventId, long userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            var cmd = new MySqlCommand("DELETE FROM Events WHERE Id = @Id AND IdUser = @IdUser", connection);
            cmd.Parameters.AddWithValue("@Id", eventId);
            cmd.Parameters.AddWithValue("@IdUser", userId);
            cmd.ExecuteNonQuery();
        }

        public void DeleteAllUserEvents(long userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            var cmd = new MySqlCommand("DELETE FROM Events WHERE IdUser = @IdUser", connection);
            cmd.Parameters.AddWithValue("@IdUser", userId);
            cmd.ExecuteNonQuery();
        }

        public List<(long userId, Events eventItem)> GetActiveEvents()
        {
            var result = new List<(long, Events)>();

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            var currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            var cmd = new MySqlCommand(
                "SELECT Id, IdUser, Message, EventTime, RepeatPattern FROM Events WHERE DATE_FORMAT(EventTime, '%Y-%m-%d %H:%i') = @CurrentTime",
                connection);

            cmd.Parameters.AddWithValue("@CurrentTime", currentTime);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var eventItem = new Events
                {
                    Id = reader.GetInt32("Id"),
                    Message = reader.GetString("Message"),
                    Time = reader.GetDateTime("EventTime"),
                    RepeatPattern = reader.IsDBNull(reader.GetOrdinal("RepeatPattern")) ? null : reader.GetString("RepeatPattern")
                };

                result.Add((reader.GetInt64("IdUser"), eventItem));
            }

            return result;
        }

        public void UpdateEventTime(int eventId, DateTime newTime)
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            var cmd = new MySqlCommand("UPDATE Events SET EventTime = @NewTime WHERE Id = @Id", connection);
            cmd.Parameters.AddWithValue("@Id", eventId);
            cmd.Parameters.AddWithValue("@NewTime", newTime);
            cmd.ExecuteNonQuery();
        }
    }
}