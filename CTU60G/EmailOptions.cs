using System;
using System.Collections.Generic;
using System.Text;

namespace CTU60G
{
    public class EmailOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string EnableSsl { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
        public IList<string> ToEmails { get; set; }
    }
}