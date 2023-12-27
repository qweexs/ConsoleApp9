using MySql.Data.MySqlClient;
using System;
using System.Net.Http;
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
           "database=Проверка");

        static async Task Main()
        {
            string botToken = "6578935983:AAGO555UWlMT9dZPju25dAApoCU3_ZCg528";

            botClient = new TelegramBotClient(botToken);
            var me = await botClient.GetMeAsync();

            Console.Title = me.Username;

            botClient.OnMessage += Bot_OnMessage;

            // Запускаем асинхронные методы
            await Task.WhenAll(
                MessageCheckThreadAsync(),
                DatabaseCountThreadAsync()
            );

            botClient.StopReceiving();
        }

        private static async void Bot_OnMessage(object sender, MessageEventArgs e)
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
                    await botClient.SendTextMessageAsync(
                        chatId: e.Message.Chat,
                        text: $"Запись добавлена в базу данных. Всего записей: {await GetDatabaseCountAsync()}"
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

        private static async Task MessageCheckThreadAsync()
        {
            while (true)
            {
                await CheckForNewMessagesAsync();
                await Task.Delay(1500);
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

        private static async Task DatabaseCountThreadAsync()
        {
            while (true)
            {
                // Получаем количество записей в базе
                int databaseCount = await GetDatabaseCountAsync();

                // Отправляем количество записей в чат бота
                await botClient.SendTextMessageAsync(
                    chatId: "-1002132656981", // Замените на ваш ID чата или имя бота
                    text: $"Количество записей в базе: {databaseCount}"
                );

                // Ожидаем одну час перед следующей отправкой
                await Task.Delay(TimeSpan.FromHours(1));
            }
        }

        private static async Task<int> GetDatabaseCountAsync()
        {
            try
            {
                _connection.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM Сообщения", _connection))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
            finally
            {
                _connection.Close();
            }
        }
    }
}
