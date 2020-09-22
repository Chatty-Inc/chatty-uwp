using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;
using System.ComponentModel;
using Firebase.Database;
using Firebase.ConsoleChat;
using Firebase.Database.Query;
using Windows.UI.Notifications;
using System.Reactive.Linq;
using Windows.ApplicationModel.VoiceCommands;
using Windows.UI.Core;
using Windows.UI.Text;
using System.Threading.Tasks;
using Firebase.Database.Offline;
using FirebaseAdmin.Messaging;
using System.Net;
using Windows.UI;
using muxc = Microsoft.UI.Xaml.Controls;
using Windows.Services.Maps.LocalSearch;
using System.Reflection.Metadata.Ecma335;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Firebase.ConsoleChat
{
    using Newtonsoft.Json;

    public class MessageBase
    {
        public string Author { get; set; }

        public string Content { get; set; }
    }

    /// <summary>
    /// Outbound message has a <see cref="ServerTimeStamp"/> to indicated that this is a server set property.
    /// </summary>
    public class OutboundMessage : MessageBase
    {
        [JsonProperty("Timestamp")]
        public ServerTimeStamp TimestampPlaceholder { get; } = new ServerTimeStamp();
    }

    /// <summary>
    /// Inbound message has <see cref="Timestamp"/> in a form of a UNIX timestamp.
    /// </summary>
    public class InboundMessage : MessageBase
    {
        public long Timestamp { get; set; }
    }

    public class ServerTimeStamp
    {
        [JsonProperty(".sv")]
        public string TimestampPlaceholder { get; } = "timestamp";
    }
}

namespace Chatty
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public sealed partial class MainPage : Page
    {
        private ChildQuery childObj = null;
        private readonly Windows.Storage.ApplicationDataContainer localSettings = 
            Windows.Storage.ApplicationData.Current.LocalSettings;
        string uuid = null;
        private Windows.Storage.ApplicationDataCompositeValue composite = new Windows.Storage.ApplicationDataCompositeValue();

        public MainPage()
        {
            this.InitializeComponent();
        }

        bool HasNonASCIIChars(string str) // Ensure user does not try to post non-printable chars
        {
            // Takes way too long
            return (System.Text.Encoding.UTF8.GetByteCount(str) != str.Length);
        }

        private void ShowToastNotification(string title, string stringContent, int time=4)
        {
            ToastNotifier ToastNotifier = ToastNotificationManager.CreateToastNotifier();
            Windows.Data.Xml.Dom.XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            Windows.Data.Xml.Dom.XmlNodeList toastNodeList = toastXml.GetElementsByTagName("text");
            toastNodeList.Item(0).AppendChild(toastXml.CreateTextNode(title));
            toastNodeList.Item(1).AppendChild(toastXml.CreateTextNode(stringContent));
            Windows.Data.Xml.Dom.IXmlNode toastNode = toastXml.SelectSingleNode("/toast");
            Windows.Data.Xml.Dom.XmlElement audio = toastXml.CreateElement("audio");
            audio.SetAttribute("src", "ms-winsoundevent:Notification.SMS");

            ToastNotification toast = new ToastNotification(toastXml);
            toast.ExpirationTime = DateTime.Now.AddSeconds(time);
            ToastNotifier.Show(toast);
        }

        private void ShowHideLoader(bool show=true)
        {
            if (show)
            {
                sendMsgLoader.IsIndeterminate = true;
                sendMsgLoader.Visibility = Visibility.Visible;
            }
            else
            {
                sendMsgLoader.IsIndeterminate = false;
                sendMsgLoader.Visibility = Visibility.Collapsed;
            }
        }

        private async void MsgTextbox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key.Equals(Windows.System.VirtualKey.Enter))
            {
                ShowHideLoader();
                var msg = msgTextbox.Text;
                if (string.IsNullOrEmpty(msg) || string.IsNullOrWhiteSpace(msg))
                {
                    msgBoxFlyoutText.Text = "Your message is empty";
                    FlyoutBase.ShowAttachedFlyout(msgTextbox);
                    ShowHideLoader(false);
                    return;
                }
                msgTextbox.Text = ""; // Clear messagebox
                await childObj.PostAsync(new OutboundMessage { Author = uuid, Content = msg });

                ShowHideLoader(false);
                e.Handled = true;
            }
        }
        private void WriteToSinkhole()
        {
            childObj.Child("sinkhole").PostAsync("Zerui");
        }

        bool hasClearedOnce = false;

        private void Messages_Loaded(object sender, RoutedEventArgs e)
        {
            childObj = new FirebaseClient("https://chatty-inc.firebaseio.com/",
                new FirebaseOptions
                {
                    OfflineDatabaseFactory = (t, s) => new OfflineDatabase(t, s),
                    AuthTokenAsyncFactory = () => Task.FromResult("mtir9VpHBO8sts4gDM8HKsWkna6mwGaKs0GJavnu")
                }).Child("messages");
            //var observableDatabase = childObj.AsRealtimeDatabase<InboundMessage>("", "", StreamingOptions.LatestOnly, InitialPullStrategy.Everything, true)
            //    .AsObservable();

            var observableDatabase = childObj.AsObservable<InboundMessage>();

            uuid = (string)localSettings.Values["userID"];
            if (uuid == null)
            {
                uuid = Guid.NewGuid().ToString();
                localSettings.Values["userID"] = uuid;
                ShowToastNotification("Chatty Debug", "UUID: " + localSettings.Values["userID"]);
            }

            // Subscribe to messages comming in, ignoring the ones that are from me
            var subscriptionSender = observableDatabase
                .Where(f => !string.IsNullOrEmpty(f.Key)) // you get empty Key when there are no data on the server for specified node
                .Where(f => f.Object?.Author != uuid)
                .Subscribe(f => AddListItem(f.Object, true));

            // Listen to the user's messages too
            var subscriptionMine = observableDatabase
                .Where(f => !string.IsNullOrEmpty(f.Key))
                .Where(f => f.Object?.Author == uuid)
                .Subscribe(f => AddListItem(f.Object, true));
            hasClearedOnce = false;
            WriteToSinkhole();
        }

        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private async void AddListItem(Firebase.ConsoleChat.InboundMessage fObject, bool fromMe = false)
        {
            if (fObject == null)
            {
                messages.Items.Clear();
                return;
            }
            string author = fObject.Author; string content = fObject.Content; long timestamp = fObject.Timestamp;
            if (!hasClearedOnce) {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    messages.Items.Clear();
                });
                hasClearedOnce = true;
            }

            var textAlignment = HorizontalAlignment.Left;
            if (author.Equals(uuid)) textAlignment = HorizontalAlignment.Right;
            if (string.IsNullOrEmpty(author)) author = "Author Unknown";
            else if (author.Equals(uuid)) author = "You";
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ListViewItem item = new ListViewItem();
                TextBlock usertext = new TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap
                };
                TextBlock username = new TextBlock
                {
                    Text = author,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };
                CornerRadius radius = new CornerRadius(4, 4, 4, 0);
                if (author.Equals("You")) radius = new CornerRadius(4, 4, 0, 4);
                var color = new SolidColorBrush(Color.FromArgb(255, 7, 94, 84));
                if (!author.Equals("You")) color = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60));
                StackPanel holder = new StackPanel
                {
                    Background = color,
                    CornerRadius = radius,
                    HorizontalAlignment = textAlignment,
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 0, 4)
                };
                if (!author.Equals("You")) holder.Children.Add(username);
                holder.Children.Add(usertext);
                item.Content = holder;
                messages.Items.Add(item);
            });
        }

        private void NavigationView_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            var clickItemName = args.InvokedItemContainer.Name.ToString();
            if (args.IsSettingsInvoked)
            {
            }
        }

        private async void joinGrpClicked(object sender, TappedRoutedEventArgs e)
        {
            var result = await joinGrpDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (!(string.IsNullOrEmpty(newGrpName.Text) || string.IsNullOrWhiteSpace(newGrpName.Text))) {
                    Windows.Storage.ApplicationDataContainer container =
                        localSettings.CreateContainer("container", Windows.Storage.ApplicationDataCreateDisposition.Always);
                    var generatedGUID = Guid.NewGuid().ToString();
                    localSettings.Containers["container"].Values[generatedGUID] = newGrpName.Text;
                    NavMain.MenuItems.Add(new muxc.NavigationViewItem
                    {
                        Content = localSettings.Containers["container"].Values[generatedGUID],
                        Icon = new SymbolIcon((Symbol)0xE716),
                        Tag = generatedGUID
                    });
                }
                else
                {
                    
                }
            }
        }

        private void NavMain_Loaded(object sender, RoutedEventArgs e)
        {
            if (localSettings.Containers.ContainsKey("container"))
            {
                foreach (var singleObj in localSettings.Containers["container"].Values)
                {
                    NavMain.MenuItems.Add(new muxc.NavigationViewItem
                    {
                        Content = singleObj.Value,
                        Icon = new SymbolIcon((Symbol)0xE716),
                        Tag = singleObj.Key
                    });
                }
            }
        }
    }
}
