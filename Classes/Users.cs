using System;
using System.Collections.Generic;

namespace TaskManagerTelegramBot_Vinokurov.Classes
{
    public class Users
    {
        public long IdUser { get; set; }
        public List<Events> Events { get; set; }
        public Users()
        {
            Events = new List<Events>();
        }
        public Users(long idUser) : this()
        {
            IdUser = idUser;
        }
    }
}