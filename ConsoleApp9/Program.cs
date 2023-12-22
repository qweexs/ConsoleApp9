using MySql.Data.MySqlClient;
using System;
using System.Data.SqlClient;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;

namespace ConsoleApp9
{
    class Program
    {
        private static ITelegramBotClient botClient;
        private static MySqlConnection _connection = new MySqlConnection("server=localhost;port=3306;username=root;password=;" +
           "database=Бот");

        static void Main()
        {
            string botToken = "6917692549:AAFa0LUb62V0yKN5V7uX6f-LnSENQvKdyRc";

            botClient = new TelegramBotClient(botToken);
            var me = botClient.GetMeAsync().Result;

            Console.Title = me.Username;

            botClient.OnMessage += Bot_OnMessage;

            // Запускаем отдельный поток для проверки сообщений каждую секунду
            Thread messageCheckThread = new Thread(MessageCheckThread);
            messageCheckThread.Start();

            // Запускаем отдельный поток для вывода количества записей в базе каждый час
            Thread databaseCountThread = new Thread(DatabaseCountThread);
            databaseCountThread.Start();

            botClient.StartReceiving();
            Console.WriteLine($"Бот {me.Username} запущен. Нажмите [Enter], чтобы завершить.");
            Console.ReadLine();

            botClient.StopReceiving();
        }

        private static void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Text != null && e.Message.Chat.Type == ChatType.Group)
            {
                Console.WriteLine($"Получено сообщение от {e.Message.Chat.Title}: {e.Message.Text}");

                // Проверяем, есть ли такое сообщение в базе данных
                if (!MessageExists(e.Message.MessageId))
                {
                    // Если нет, добавляем его
                    InsertMessage(e.Message);

                    // Отправляем ответное сообщение в группу
                    botClient.SendTextMessageAsync(
                        chatId: e.Message.Chat,
                        text: $"Запись добавлена в базу данных. Всего записей: {GetDatabaseCount()}"
                    );
                }
            }
        }

        private static bool MessageExists(int messageId)
        {
            try
            {
                _connection.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM Messages WHERE MessageId = @MessageId", _connection))
                {
                    cmd.Parameters.AddWithValue("@MessageId", messageId);
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
            finally
            {
                _connection.Close();
            }
        }

        private static void InsertMessage(Telegram.Bot.Types.Message message)
        {
            try
            {
                _connection.Open();

                using (MySqlCommand cmd = new MySqlCommand("INSERT INTO Messages (MessageId, ChatId, Username, Text, Date) VALUES (@MessageId, @ChatId, @Text, @Date)", _connection))
                {
                    cmd.Parameters.AddWithValue("@MessageId", message.MessageId);
                    cmd.Parameters.AddWithValue("@ChatId", message.Chat.Id);
                    cmd.Parameters.AddWithValue("@Text", message.Text);
                    cmd.Parameters.AddWithValue("@Date", message.Date);

                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                _connection.Close();
            }
        }

        private static async void MessageCheckThread()
        {
            while (true)
            {
                await CheckForNewMessagesAsync();
                await Task.Delay(1000);
            }
        }

        private static async Task CheckForNewMessagesAsync()
        {
            try
            {
                var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(2); // Установка тайм-аута в 2 минуты

                var updates = await botClient.GetUpdatesAsync();

                foreach (var update in updates)
                {
                    // Обработка новых сообщений
                    if (update.Message != null)
                    {
                        Bot_OnMessage(null, new MessageEventArgs(update.Message));
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Обработка отмены задачи из-за тайм-аута
                Console.WriteLine("Задача отменена из-за тайм-аута.");
            }
        }

        private static async void DatabaseCountThread()
        {
            while (true)
            {
                // Получаем количество записей в базе
                int databaseCount = GetDatabaseCount();

                // Отправляем количество записей в чат бота
                await botClient.SendTextMessageAsync(
                    chatId: "-1002004508817", // Замените на ваш ID чата или имя бота
                    text: $"Количество записей в базе: {databaseCount}"
                );

                // Ожидаем один час перед следующей отправкой
                await Task.Delay(TimeSpan.FromHours(1));
            }
        }

        private static int GetDatabaseCount()
        {
            try
            {
                _connection.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM История_сообщений", _connection))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            finally
            {
                _connection.Close();
            }
        }
    }
}
