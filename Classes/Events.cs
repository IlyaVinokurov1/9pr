using System;

namespace TaskManagerTelegramBot_Vinokurov.Classes
{
    public class Events
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public string Message { get; set; }
        public Events()
        {
        }
        public Events(DateTime time, string message)
        {
            Time = time;
            Message = message;
        }
    }
}