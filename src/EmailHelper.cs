using System;
using System.Net.Mail;
using NLog;

namespace APFy.me.utilities
{
    public class EmailHelper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static bool SendEmail(string from, string to, string subject, string body) {
            MailMessage mess = new MailMessage(from, to, subject, body);

            return SendEmail(mess);
        }

        public static bool SendEmail(MailMessage mess)
        {
            logger.Info(() => string.Format("From: {0}, Message: {1}", mess.From.Address, mess.Body));

            try
            {
                SmtpClient smtp = new SmtpClient();
                smtp.Send(mess);
            }
            catch (Exception e)
            {
                //Log warning
                logger.WarnException("E-mail failed", e);
                return false;
            }
            return true;
        }
    }
}
