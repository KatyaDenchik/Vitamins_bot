using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace VitaminStoreBot
{
    public class Bot
    {
        private readonly ITelegramBotClient _botClient;
        private readonly BotConfig _config;
        private readonly List<Product> _products;
        private readonly Dictionary<long, Dictionary<string, int>> _carts;
        private readonly Dictionary<long, Order> _pendingOrders;
        private readonly Dictionary<long, int> _cartHeaderMessageId;
        private readonly Dictionary<long, Dictionary<string, int>> _cartMessageIds;
        private readonly Dictionary<long, int> _totalMessageId;
        private readonly Dictionary<long, AdminRegistration> _pendingAdminRegistrations;

        public Bot(BotConfig config)
        {
            _config = config;
            _botClient = new TelegramBotClient(_config.Token);
            _products = GetProduct();
            _carts = new Dictionary<long, Dictionary<string, int>>();
            _pendingOrders = new Dictionary<long, Order>();
            _cartHeaderMessageId = new Dictionary<long, int>();
            _cartMessageIds = new Dictionary<long, Dictionary<string, int>>();
            _totalMessageId = new Dictionary<long, int>();
            _pendingAdminRegistrations = new Dictionary<long, AdminRegistration>();
        }

        public void Start()
        {
            var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                ThrowPendingUpdates = true
            };

            _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);
            Console.WriteLine("Bot started");
            while (true) { }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message.Type == MessageType.Text)
            {
                await HandleMessage(botClient, update, update.Message);
                return;
            }
            if (update.Type == UpdateType.CallbackQuery)
            {
                await HandleCallbackQuery(botClient, update.CallbackQuery);
                return;
            }
        }

        private async Task HandleMessage(ITelegramBotClient client, Update update, Message message)
        {
            var chatId = update.Message.Chat.Id;
            var text = message.Text;

            if (text == "/start")
            {
                await SendWelcomeMessageAsync(chatId);
                Console.WriteLine($"Chat ID: {chatId}");
            }
            else if (text == _config.SecretWord)
            {
                await RegisterAdmin(chatId);
                return;
            }
            else if (_pendingOrders.ContainsKey(chatId))
            {
                await ProcessOrder(chatId, text);
            }
        }

        private async Task RegisterAdmin(long chatId)
        {
                _config.SaveAdmin(chatId);
                await _botClient.SendTextMessageAsync(chatId, "Вы успешно зарегистрированы как администратор.");
        }

        private async Task ProcessOrder(long chatId, string text)
        {
            var order = _pendingOrders[chatId];
            if (string.IsNullOrEmpty(order.CustomerName))
            {
                order.CustomerName = text;
                await _botClient.SendTextMessageAsync(chatId, "Введите вашу фамилию:");
            }
            else if (string.IsNullOrEmpty(order.CustomerSurname))
            {
                order.CustomerSurname = text;
                await _botClient.SendTextMessageAsync(chatId, "Введите ваше отчество:");
            }
            else if (string.IsNullOrEmpty(order.CustomerPatronymic))
            {
                order.CustomerPatronymic = text;
                await _botClient.SendTextMessageAsync(chatId, "Введите ваш номер телефона:");
            }
            else if (string.IsNullOrEmpty(order.CustomerPhone))
            {
                order.CustomerPhone = text;
                await _botClient.SendTextMessageAsync(chatId, "Выберите способ оплаты:", replyMarkup: GetPaymentOptionsKeyboard());
            }
            else if (string.IsNullOrEmpty(order.PaymentMethod))
            {
                order.PaymentMethod = text;
                await _botClient.SendTextMessageAsync(chatId, "Введите адрес доставки:");
            }
            else if (string.IsNullOrEmpty(order.DeliveryAddress))
            {
                order.DeliveryAddress = text;
                await FinalizeOrder(chatId);
            }
        }

        private async Task HandleCallbackQuery(ITelegramBotClient client, CallbackQuery callbackQuery)
        {
            var chatId = callbackQuery.Message.Chat.Id;

            if (callbackQuery.Data.Contains("Показати товари"))
            {
                await ShowProductsAsync(chatId);
            }
            if (callbackQuery.Data.Contains("Кошик"))
            {
                await ShowCartAsync(chatId, false);
            }
            if (callbackQuery.Data.Contains("Назад до товарів"))
            {
                await ShowProductsAsync(chatId);
            }
            if (callbackQuery.Data.StartsWith("Додати до кошику:"))
            {
                var productName = callbackQuery.Data.Split(':')[1];
                await AddToCartAsync(chatId, productName);
            }
            else if (callbackQuery.Data.StartsWith("Збільшити:"))
            {
                var productName = callbackQuery.Data.Split(':')[1];
                await UpdateCartAsync(chatId, productName, 1);
            }
            else if (callbackQuery.Data.StartsWith("Зменшити:"))
            {
                var productName = callbackQuery.Data.Split(':')[1];
                await UpdateCartAsync(chatId, productName, -1);
            }
            else if (callbackQuery.Data.StartsWith("Видалити:"))
            {
                var productName = callbackQuery.Data.Split(':')[1];
                await RemoveFromCartAsync(chatId, productName);
            }
            else if (callbackQuery.Data.Contains("Оформити замовлення"))
            {
                await StartOrderProcess(chatId);
            }
            else if (callbackQuery.Data.Contains("Накладений платіж") || callbackQuery.Data.Contains("Оплата на карту"))
            {
                _pendingOrders[chatId].PaymentMethod = callbackQuery.Data;
                await _botClient.SendTextMessageAsync(chatId, "Введіть адресу доставки:");
            }
            else
            {
                await HandleProductSelectionAsync(chatId, callbackQuery.Data);
            }
        }

        private async Task AddToCartAsync(long chatId, string productName)
        {
            var selectedProduct = _products.FirstOrDefault(p => p.Name == productName);
            if (selectedProduct != null)
            {
                if (!_carts.ContainsKey(chatId))
                {
                    _carts[chatId] = new Dictionary<string, int>();
                }

                if (_carts[chatId].ContainsKey(productName))
                {
                    _carts[chatId][productName]++;
                }
                else
                {
                    _carts[chatId][productName] = 1;
                }

                await _botClient.SendTextMessageAsync(chatId, $"{selectedProduct.Name} додано до кошику.");
            }
        }

        private async Task UpdateCartAsync(long chatId, string productName, int change)
        {
            if (_carts.ContainsKey(chatId) && _carts[chatId].ContainsKey(productName))
            {
                _carts[chatId][productName] += change;
                if (_carts[chatId][productName] <= 0)
                {
                    _carts[chatId].Remove(productName);
                    if (_cartMessageIds.ContainsKey(chatId) && _cartMessageIds[chatId].ContainsKey(productName))
                    {
                        var messageId = _cartMessageIds[chatId][productName];
                        await _botClient.DeleteMessageAsync(chatId, messageId);
                        _cartMessageIds[chatId].Remove(productName);
                    }
                }
                await ShowCartAsync(chatId);
            }
        }

        private async Task RemoveFromCartAsync(long chatId, string productName)
        {
            if (_carts.ContainsKey(chatId) && _carts[chatId].ContainsKey(productName))
            {
                _carts[chatId].Remove(productName);
                if (_cartMessageIds.ContainsKey(chatId) && _cartMessageIds[chatId].ContainsKey(productName))
                {
                    var messageId = _cartMessageIds[chatId][productName];
                    await _botClient.DeleteMessageAsync(chatId, messageId);
                    _cartMessageIds[chatId].Remove(productName);
                }
                await ShowCartAsync(chatId);
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Error: {exception.Message}");
            return Task.CompletedTask;
        }

        //WelcomeMessage

        private async Task SendWelcomeMessageAsync(long chatId)
        {
            var welcomeMessage = "GEN - Gold Electrum Nutrition – це компанія з інноваційним підходом до здоров'я, яка пропонує продукти на основі передових наукових досліджень і високоякісних інгредієнтів для підтримки вашого організму у відмінному стані.\r\n\r\n" +
                                 "Об'єднання експертів: ми залучили та об'єднали висококваліфікованих науковців та фахівців з усього світу для розробки інноваційних продуктів.\r\n\r\nПередові дослідження: наші фахівці створили органо-мінеральний комплекс на основі" +
                                 " сучасних наукових досліджень.\r\n\r\nСила магнію: основа комплексу - магнієва сіль природної бурштинової кислоти - сукцинат. Ця форма забезпечує найшвидше транспортування іонів магнію до внутрішньоклітинних процесів.\r\n\r\nКомплексний" +
                                 " підхід: до складу комплексу також входять необхідні мікроелементи, які створюють базу для 750 біохімічних процесів у вашому організмі.\r\n\r\nЧас подбати про себе: Саме зараз час віддати своєму здоров'ю заслужену увагу та турботу.";
            var photoPath = @"WelcomePhoto.png";

                await _botClient.SendPhotoAsync(chatId, InputFile.FromStream(System.IO.File.Open(photoPath, FileMode.Open)));
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("Показати товари", "Показати товари"),
                InlineKeyboardButton.WithCallbackData("Кошик", "Кошик")
            });

            await _botClient.SendTextMessageAsync(chatId, welcomeMessage, replyMarkup: keyboard);
        }

        //ShowProduct

        private List<Product>? GetProduct()
        {
            return new List<Product>
    {
        new Product
        {
            Name = "Магній 500 PRO",
            Description = "Склад 1 капсули:\r\n" +
            "- 500мг магній iз сукцинату та карбонату \r\n" +
            "- 12мг цинк iз цитрату \r\n- 1.5мг мідь iз сукцинату\r\n" +
            "- 30мг фульвові кислоти \r\n- 20мг кальцiй iз ацетату \r\n" +
            "- 20мг вітамін В6 \r\n- 120 мкг (4800 МЕ) вітамін D3\r\n" +
            "В банці 90 капсул\r\n\r\nДопоміжні інгредієнти:\r\n" +
            "Не містить:\r\n\r\nПшениці\r\nГлютену\r\nСої\r\nМолока\r\nЯєць\r\nРиби\r\n" +
            "Молюсків чи деревних горіхів",
            Price = 495,
            Count = 100,
            ImagePath = @"PRO.png"
        },
        new Product
        {
            Name = "Калій ULTRA",
            Description = "Склад 1 капсули:\r\n" +
            "- 1200мг калій з бiкарбонату та аскорбату \r\n\r\n" +
            "- 400мг вітамін С \r\n\r\n- стеаринова кислота\r\n\r\n" +
            "- діоксид кремнію\r\n\r\n \r\n\r\nВ банці 90 капсул\r\n\r\n" +
            "Допоміжні інгредієнти:\r\n" +
            "Не містить:\r\n\r\nПшениці\r\nГлютену\r\nСої\r\nМолока\r\nЯєць\r\nРиби\r\n" +
            "Молюсків чи деревних горіхів",
            Price = 495,
            Count = 100,
            ImagePath = @"ULTRA.png"
        },
        new Product
        {
            Name = "HEALTH KIT",
            Description = "Склад 1 капсули:\r\n" +
            "Магній 500 PRO\r\n- 500мг магній iз сукцинату та карбонату \r\n" +
            "- 12мг цинк iз цитрату \r\n- 1.5мг мідь iз сукцинату\r\n" +
            "- 30мг фульвові кислоти \r\n- 20мг кальцiй iз ацетату \r\n" +
            "- 20мг вітамін В6 \r\n- 120 мкг (4800 МЕ) вітамін D3\r\n " +
            "\r\nКалій ULTRA\r\n- 1200мг калій з бiкарбонату та аскорбату \r\n\r\n" +
            "- 400мг вітамін С \r\n\r\n- стеаринова кислота\r\n\r\n- діоксид кремнію\r\n\r\n" +
            " \r\n\r\nВ наборі дві банки по 90 капсул в кожній\r\n\r\n" +
            "Допоміжні інгредієнти:\r\n" +
            "Не містить:\r\n\r\nПшениці\r\nГлютену\r\nСої\r\nМолока\r\nЯєць\r\nРиби\r\n" +
            "Молюсків чи деревних горіхів",
            Price = 849,
            Count = 100,
            ImagePath = @"KIT.png"
        }

    };
        }

        private async Task HandleProductSelectionAsync(long chatId, string productName)
        {
            var selectedProduct = _products.FirstOrDefault(p => p.Name == productName);
            if (selectedProduct != null)
            {
                using (var stream = new FileStream(selectedProduct.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var inputFile = new InputFileStream(stream, selectedProduct.ImagePath);

                    await _botClient.SendPhotoAsync(chatId, inputFile);
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData("Додати до кошику", $"Додати до кошику:{selectedProduct.Name}"),
                            InlineKeyboardButton.WithCallbackData("Назад", "Назад до товарів")
                        },
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData("Кошик", "Кошик")
                        }
                    });

                    await _botClient.SendTextMessageAsync(chatId, $"{selectedProduct.Name}\n{selectedProduct.Description}\nЦіна: {selectedProduct.Price} грн.", replyMarkup: keyboard);
                }
            }
        }

        private async Task ShowProductsAsync(long chatId)
        {
            var keyboardButtons = _products.Select(product =>
                InlineKeyboardButton.WithCallbackData(product.Name, product.Name)
            ).ToList();

            var keyboardAfter = new[]
            {
                InlineKeyboardButton.WithCallbackData("Кошик", "Кошик")
            };

            keyboardButtons.AddRange(keyboardAfter);

            var keyboard = new InlineKeyboardMarkup(keyboardButtons.Select(button => new[] { button }));

            await _botClient.SendTextMessageAsync(chatId, "Оберіть товар:", replyMarkup: keyboard);
        }

        private async Task ShowCartAsync(long chatId, bool needUpdate = true)
        {
                if (_carts.ContainsKey(chatId) && _carts[chatId].Any())
                {
                    if (!_cartMessageIds.ContainsKey(chatId))
                    {
                        _cartMessageIds[chatId] = new Dictionary<string, int>();
                    }

                    // Обновление сообщения заголовка корзины
                    if (_cartHeaderMessageId.ContainsKey(chatId) && needUpdate)
                    {
                        await UpdateMessageAsync(chatId, _cartHeaderMessageId[chatId], "*Ваш кошик:*", parseMode: ParseMode.Markdown);
                    }
                    else
                    {
                        var cartHeaderMessage = await _botClient.SendTextMessageAsync(chatId, "*Ваш кошик:*", parseMode: ParseMode.Markdown);
                        _cartHeaderMessageId[chatId] = cartHeaderMessage.MessageId;
                    }

                    // Обновление сообщений для каждого товара в корзине
                    foreach (var item in _carts[chatId])
                    {
                        var product = _products.FirstOrDefault(p => p.Name == item.Key);
                        if (product != null)
                        {
                            var message = $"*{item.Key}*\n" +
                                          $"Кількість: {item.Value}\n" +
                                          $"Ціна за одиницю: {product.Price}\n" +
                                          $"Ціна всього: {product.Price * item.Value}";

                            var keyboard = new InlineKeyboardMarkup(new[]
                            {
                    InlineKeyboardButton.WithCallbackData("➕", $"Збільшити:{item.Key}"),
                    InlineKeyboardButton.WithCallbackData("➖", $"Зменшити:{item.Key}"),
                    InlineKeyboardButton.WithCallbackData("🗑", $"Видалити:{item.Key}")
                });

                            if (_cartMessageIds[chatId].ContainsKey(item.Key) && needUpdate)
                            {
                                var messageId = _cartMessageIds[chatId][item.Key];
                                await UpdateMessageAsync(chatId, messageId, message, parseMode: ParseMode.Markdown, replyMarkup: keyboard);
                            }
                            else
                            {
                                var sentMessage = await _botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, replyMarkup: keyboard);
                                _cartMessageIds[chatId][item.Key] = sentMessage.MessageId;
                            }
                        }
                    }

                    var total = _carts[chatId].Sum(item => _products.FirstOrDefault(p => p.Name == item.Key)?.Price * item.Value);
                    var totalMessage = $"*В сумі: {total} грн*";

                    if (_totalMessageId.ContainsKey(chatId) && needUpdate)
                    {
                        await UpdateMessageAsync(chatId, _totalMessageId[chatId], totalMessage, parseMode: ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(new[]
                        {
                InlineKeyboardButton.WithCallbackData("Оформити замовлення", "Оформити замовлення")
            }));
                    }
                    else
                    {
                        var totalSentMessage = await _botClient.SendTextMessageAsync(chatId, totalMessage, parseMode: ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(new[]
                        {
                InlineKeyboardButton.WithCallbackData("Оформити замовлення", "Оформити замовлення")
            }));
                        _totalMessageId[chatId] = totalSentMessage.MessageId;
                    }
                }
                else
                {
                    await _botClient.SendTextMessageAsync(chatId, "Ваш кошик порожній.");
                    if (_cartHeaderMessageId.ContainsKey(chatId))
                    {
                        await _botClient.DeleteMessageAsync(chatId, _cartHeaderMessageId[chatId]);
                        _cartHeaderMessageId.Remove(chatId);
                    }

                    if (_totalMessageId.ContainsKey(chatId))
                    {
                        await _botClient.DeleteMessageAsync(chatId, _totalMessageId[chatId]);
                        _totalMessageId.Remove(chatId);
                    }

                    if (_cartMessageIds.ContainsKey(chatId))
                    {
                        foreach (var messageId in _cartMessageIds[chatId].Values)
                        {
                            await _botClient.DeleteMessageAsync(chatId, messageId);
                        }
                        _cartMessageIds.Remove(chatId);
                    }
                }
            }

        private async Task UpdateMessageAsync(long chatId, int messageId, string text, ParseMode parseMode = ParseMode.Markdown, InlineKeyboardMarkup replyMarkup = null)
        {
            try
            {
                await _botClient.EditMessageTextAsync(chatId, messageId, text, parseMode, replyMarkup: replyMarkup);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("message is not modified"))
                { 
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task StartOrderProcess(long chatId)
        {
            if (_carts.ContainsKey(chatId) && _carts[chatId].Any())
            {
                _pendingOrders[chatId] = new Order
                {
                    ChatId = chatId,
                    Cart = _carts[chatId]
                };

                await _botClient.SendTextMessageAsync(chatId, "Введіть ваше ім'я:");
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, "Ваш кошик порожній.");
            }
        }

        private async Task FinalizeOrder(long chatId)
        {
            var order = _pendingOrders[chatId];

            var orderDetails = new
            {
                order.CustomerName,
                order.CustomerSurname,
                order.CustomerPatronymic,
                order.CustomerPhone,
                order.PaymentMethod,
                order.DeliveryAddress,
                Cart = order.Cart.Select(item =>
                {
                    var product = _products.FirstOrDefault(p => p.Name == item.Key);
                    return new
                    {
                        ProductName = item.Key,
                        Quantity = item.Value,
                        UnitPrice = product?.Price,
                        TotalPrice = product?.Price * item.Value
                    };
                }),
                TotalPrice = order.Cart.Sum(item => _products.FirstOrDefault(p => p.Name == item.Key)?.Price * item.Value)
            };

            var jsonFileName = $"{order.CustomerName}_{order.CustomerSurname}_{DateTime.Now:yyyyMMddHHmmss}.json";
            var json = JsonConvert.SerializeObject(orderDetails, Formatting.Indented);
            System.IO.File.WriteAllText(jsonFileName, json);

            var adminMessage = $"Нове замовлення від {order.CustomerName} {order.CustomerSurname} {order.CustomerPatronymic}\n" +
                              $"Телефон: {order.CustomerPhone}\n" +
                              $"Вид оплати: {order.PaymentMethod}\nАдреса доставки: {order.DeliveryAddress}\n\n";

            foreach (var item in order.Cart)
            {
                var product = _products.FirstOrDefault(p => p.Name == item.Key);
                if (product != null)
                {
                    adminMessage += $"{item.Key} - Кількість: {item.Value}, Ціна за одиницю: {product.Price}, Ціна всього: {product.Price * item.Value}\n";
                }
            }

            adminMessage += $"\nЗагальна сума: {order.Cart.Sum(item => _products.FirstOrDefault(p => p.Name == item.Key)?.Price * item.Value)} грн";

            foreach (var admin in _config.Admins)
            {
                await _botClient.SendTextMessageAsync(admin, adminMessage);
            }

            await _botClient.SendTextMessageAsync(chatId, "Ваше замовлення було оформлено!");

            _pendingOrders.Remove(chatId);
            _carts.Remove(chatId);
        }

        private InlineKeyboardMarkup GetPaymentOptionsKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("Накладений платіж", "Накладений платіж"),
                InlineKeyboardButton.WithCallbackData("Оплата на карту", "Оплата на карту")
            });
        }
    }
}
