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
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using System.Security.Cryptography;
using MyToolkit.Multimedia;
using yt=YouTubeSearch;
using System.Text.RegularExpressions;
using Windows.Media.Capture.Frames;
using Firebase.Database.Streaming;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Firebase.ConsoleChat
{
    using Newtonsoft.Json;
    public class InvitationMgmt
    {
        public string GrpUUID { get; set; }
    }

    public class AcceptedInvitation
    {
        public string EncryptedAES { get; set; }
        public string GrpName { get; set; }
    }

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
        private string selectedGrpTag = null;
        private ChildQuery childObj = null;
        FirebaseClient rootDatabase = null;
        private int charsPostedInMin = 0;
        private int brokeRulesTimes = 0;
        private int suspicion = 0; // Give captcha when this reaches 10
        private System.DateTime startTime = DateTime.UtcNow;
        private readonly Windows.Storage.ApplicationDataContainer localSettings = 
            Windows.Storage.ApplicationData.Current.LocalSettings;
        string uuid = null;
        private Windows.Storage.ApplicationDataCompositeValue composite = new Windows.Storage.ApplicationDataCompositeValue();

        static string EncryptMsg(string plainText, string strKey, string strIV)
        {
            // Convert all params to byte arrays
            byte[] Key = Convert.FromBase64String(strKey);
            byte[] IV = Convert.FromBase64String(strIV);
            byte[] encrypted;

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            // Return the encrypted bytes from the memory stream.
            return Convert.ToBase64String(encrypted, 0, encrypted.Length);
        }

        static string DecryptMsg(string cipherStr, string strKey, string strIV)
        {
            try
            {
                // Convert all params to byte arrays
                byte[] Key = Convert.FromBase64String(strKey);
                byte[] IV = Convert.FromBase64String(strIV);
                byte[] cipherText = Convert.FromBase64String(cipherStr);

                // Declare the string used to hold
                // the decrypted text.
                string plaintext = null;

                // Create an Aes object
                // with the specified key and IV.
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = Key;
                    aesAlg.IV = IV;

                    // Create a decryptor to perform the stream transform.
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    // Create the streams used for decryption.
                    using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {

                                // Read the decrypted bytes from the decrypting stream
                                // and place them in a string.
                                plaintext = srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }

                return plaintext;
            }
            catch
            {
                return null;
            }
        }

        public MainPage()
        {
            this.InitializeComponent();

            foreach (var singleObj in localSettings.Containers["pendingInvites"].Values)
            {
                // Listen to the user's messages too
                subscriptionMine = rootDatabase.Child("encryptedAESKeys").Child(singleObj.Value.ToString).AsObservable<AcceptedInvitation>()
                    .Where(f => f != null)
                    .Subscribe(f => reqJoinResp(f.Object));
            }
        }


        private void reqJoinResp(AcceptedInvitation invitationResp)
        {
            if (!invitationResp.EncryptedAES.Equals("rej")) { // Invitation accepted

            }
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

        DateTime messageLastSent = DateTime.UtcNow;

        private async void MsgTextbox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key.Equals(Windows.System.VirtualKey.Enter))
            {
                var msg = msgTextbox.Text;
                if (messageLastSent.AddMilliseconds((1024 - msg.Length) * 1.25) > DateTime.UtcNow)
                {
                    suspicion += msg.Length / 100;
                }
                messageLastSent = DateTime.UtcNow;

                if (string.IsNullOrEmpty(msg) || string.IsNullOrWhiteSpace(msg))
                {
                    msgBoxFlyoutText.Text = "Your message is empty";
                    FlyoutBase.ShowAttachedFlyout(msgTextbox);
                    return;
                }
                ShowToastNotification("chars posted in min: ", charsPostedInMin.ToString());
                charsPostedInMin += msg.Length + 5; // Penalty for short messages
                if (charsPostedInMin > 5000)
                {
                    brokeRulesTimes++;
                    charsPostedInMin = 0;
                    msgBoxFlyoutText.Text = "You are being rate limited" + Environment.NewLine + "Rate limited times this session: " + brokeRulesTimes;
                    FlyoutBase.ShowAttachedFlyout(msgTextbox);
                    msgTextbox.IsEnabled = false;

                    for (int i = 0; i < brokeRulesTimes * 10; i++)
                    {
                        msgTextbox.Header = (brokeRulesTimes * 10 - i).ToString() + " seconds left";
                        // await Task.Delay(1000); // I don't want to wait the whole day to text my app
                    }

                    msgTextbox.Header = null;
                    msgTextbox.IsEnabled = true;

                    suspicion += brokeRulesTimes;
                }
                else
                {
                    ShowHideLoader();
                    msgTextbox.Text = ""; // Clear messagebox
                    await childObj.Child(selectedGrpTag).PostAsync(new OutboundMessage
                    {
                        Author = uuid,
                        Content =
                        EncryptMsg(msg, localSettings.Containers["keys"].Values[selectedGrpTag].ToString(), localSettings.Containers["iv"].Values[selectedGrpTag].ToString())
                    });
                    ShowHideLoader(false);
                }

                if (suspicion >= 15)
                {
                    brokeRulesTimes++;
                    generalDialog.Title = "Something's going on here";
 
                    msgTextbox.Header = "Please complete the captcha before sending a message";
                    msgTextbox.IsEnabled = false;

                    dialogRespText.Header = null; // Just in case

                    int attempts;
                    for (attempts = 0; attempts < 10; attempts++)
                    {
                        generalDialog.Hide();

                        int randomNumber = new Random().Next(100000); 
                        dialogContent.Text = "Fishy activity detected" + Environment.NewLine + "Please enter" + Environment.NewLine + randomNumber + Environment.NewLine + "in the text box below";

                        dialogRespText.Header = (10-attempts).ToString() + " Remaining Attempt(s)";

                        var resp = await generalDialog.ShowAsync();
                        if (resp == ContentDialogResult.Primary)
                        {
                            if (dialogRespText.Text == randomNumber.ToString())
                            {
                                msgTextbox.IsEnabled = true;
                                msgTextbox.Header = null;
                                suspicion = brokeRulesTimes/5;
                                break;
                            }
                        }
                    }

                    if (attempts >= 10)
                    {
                        msgBoxFlyoutText.Text = "Maximum captcha tries reached. Please restart app to continue sending messages";
                        FlyoutBase.ShowAttachedFlyout(msgTextbox);
                    }
                }

                if (startTime.AddMinutes(1) < DateTime.UtcNow)
                {
                    charsPostedInMin = 0; // Reset counter
                    startTime = DateTime.UtcNow;
                }
                e.Handled = true;
            }
        }

        bool hasClearedOnce = false;

        System.IDisposable subscriptionSender = null;
        System.IDisposable subscriptionMine = null;

        private void reloadList()
        {
            var observableDatabase = childObj.Child(selectedGrpTag).AsObservable<InboundMessage>();

            uuid = (string)localSettings.Values["userID"];
            if (uuid == null)
            {
                uuid = Guid.NewGuid().ToString();
                localSettings.Values["userID"] = uuid;
                ShowToastNotification("Chatty Debug", "UUID: " + localSettings.Values["userID"]);
            }

            if (subscriptionSender != null)
            {
                subscriptionSender.Dispose();
                subscriptionMine.Dispose();
            }

            // Subscribe to messages comming in, ignoring the ones that are from me
            subscriptionSender = observableDatabase
                .Where(f => !string.IsNullOrEmpty(f.Key)) // you get empty Key when there are no data on the server for specified node
                .Where(f => f.Object?.Author != uuid)
                .Subscribe(f => AddListItem(f.Object, true));

            // Listen to the user's messages too
            subscriptionMine = observableDatabase
                .Where(f => !string.IsNullOrEmpty(f.Key))
                .Where(f => f.Object?.Author == uuid)
                .Subscribe(f => AddListItem(f.Object, true));
            hasClearedOnce = false;
        }

        bool reloadedListOnce = false;

        private void Messages_Loaded(object sender, RoutedEventArgs e)
        {
            rootDatabase = new FirebaseClient("https://chatty-inc.firebaseio.com/",
                new FirebaseOptions
                {
                    OfflineDatabaseFactory = (t, s) => new OfflineDatabase(t, s),
                    AuthTokenAsyncFactory = () => Task.FromResult("mtir9VpHBO8sts4gDM8HKsWkna6mwGaKs0GJavnu")
                });
            childObj = rootDatabase.Child("messages");
            //var observableDatabase = childObj.AsRealtimeDatabase<InboundMessage>("", "", StreamingOptions.LatestOnly, InitialPullStrategy.Everything, true)
            //    .AsObservable();

            if (!reloadedListOnce)
            {
                reloadList();
                reloadedListOnce = true;
            }
        }

        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private async void AddListInvite(string rawInput)
        {
            var arrayParams = rawInput.Split(',');
            string rsaPublicKey = arrayParams[0];
            string cypherText = null;
            try
            {
                // Get the object back from seriallized string
                var pubKey = (RSAParameters)new System.Xml.Serialization.XmlSerializer(typeof(RSAParameters)).Deserialize(new StringReader(rsaPublicKey));

                // Another one for isolation from prev instances
                var csp = new RSACryptoServiceProvider();
                csp.ImportParameters(pubKey);
                // Pad and encrypt data to Base64 for transmission
                cypherText = Convert.ToBase64String(csp.Encrypt(System.Text.Encoding.Unicode
                    .GetBytes(localSettings.Containers["keys"].Values[selectedGrpTag].ToString() + "," + localSettings.Containers["iv"].Values[selectedGrpTag].ToString())
                    , false));
            }
            catch
            {
                // TODO: Show error message
                return;
            }

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                rootDatabase.Child("encryptedAESKeys").Child(arrayParams[1]).PutAsync(new AcceptedInvitation { EncryptedAES = cypherText, GrpName = grpName.Text });
                if (!hasClearedOnce)
                {
                    messages.Items.Clear();
                    hasClearedOnce = true;
                }

                ListViewItem item = new ListViewItem();
                StackPanel inviteHolder = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush(Color.FromArgb(255, 45, 45, 45)),
                    Padding = new Thickness(5),
                    CornerRadius = new CornerRadius(4)
                };
                TextBlock reqText = new TextBlock
                {
                    Text = "A user has requested to join this group." + Environment.NewLine + "Do you accept the request?",
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4),
                    FontSize = 16
                };
                StackPanel buttonHolder = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Button acceptInvite = new Button
                {
                    Content = "Accept Request",
                    Style = (Style)Resources["AccentButtonStyle"],
                    Tag = rsaPublicKey,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                Button rejectInvite = new Button
                {
                    Content = "Deny Request",
                    Tag = rsaPublicKey,
                    Margin = new Thickness(4, 0, 4, 0)
                };

                buttonHolder.Children.Add(acceptInvite);
                buttonHolder.Children.Add(rejectInvite);
                inviteHolder.Children.Add(reqText);
                inviteHolder.Children.Add(buttonHolder);
                item.Content = inviteHolder;
                messages.Items.Add(item);
            });
        }

        private async void AddListItem(InboundMessage fObject, bool fromMe = false)
        {
            if (fObject == null)
            {
                messages.Items.Clear();
                return;
            }

            // Check validity and if message is an invite
            string author = fObject.Author; long timestamp = fObject.Timestamp;
            if (author.Equals("specialGrpRequest")) AddListInvite(fObject.Content.ToString());
            string content = DecryptMsg(fObject.Content, 
                localSettings.Containers["keys"].Values[selectedGrpTag].ToString(), localSettings.Containers["iv"].Values[selectedGrpTag].ToString());
            if (string.IsNullOrEmpty(content)) return;
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

                // s.StartsWith("http://") || s.StartsWith("www.") || s.StartsWith("https://")
                var links = content.Split("\t\n ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Where(s => s.StartsWith("https://www.youtube.com/watch?"));

                foreach (string s in links)
                {
                    ShowToastNotification("Youtube Player Debug", s);
                    try
                    {
                        IEnumerable<yt.VideoInfo> videoInfos = yt.DownloadUrlResolver.GetDownloadUrls(s, false);
                        // Select the first video with sound
                        yt.VideoInfo video = videoInfos
                            .First(info => info.AudioType != yt.AudioType.Unknown);

                        // Decrypt only if needed
                        if (video.RequiresDecryption)
                        {
                            yt.DownloadUrlResolver.DecryptDownloadUrl(video);
                        }

                        Uri videoUri;
                        ShowToastNotification("YoutubeURL", video.DownloadUrl);
                        if (Uri.TryCreate(video.DownloadUrl, UriKind.Absolute, out videoUri))
                        {
                            TextBlock songTitle = new TextBlock
                            {
                                Text = video.Title,
                                FontSize = 16,
                                Margin = new Thickness(0, 0, 0, 5),
                                FontWeight = FontWeights.Bold
                            };
                            MediaElement ytPlayer = new MediaElement
                            {
                                Source = videoUri,
                                AreTransportControlsEnabled = true,
                                HorizontalAlignment = textAlignment,
                                AutoPlay = false,
                                AudioCategory = AudioCategory.BackgroundCapableMedia
                            };
                            holder.Children.Add(songTitle);
                            holder.Children.Add(ytPlayer);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                    break;
                }

                // Check if string contains URLs
                foreach (Match matchedURL in Regex.Matches(fObject.Content, @"(http|ftp|https):\/\/([\w\-_]+(?:(?:\.[\w\-_]+)+))([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?"))
                {
                    Console.WriteLine(matchedURL.Value);
                    
                }

                holder.Children.Add(usertext);
                item.Content = holder;
                messages.Items.Add(item);
            });
        }

        private async void NavigationView_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            var clickItemTag = args.InvokedItemContainer.Tag;
            if (args.IsSettingsInvoked)
            {
                await settingsDialog.ShowAsync();
            }
            else if (clickItemTag != null)
            {
                selectedGrpTag = clickItemTag.ToString();
                grpName.Text = args.InvokedItemContainer.Content.ToString();
                messages.Items.Clear();
                reloadList();
            }
        }

        private async void joinGrpClicked(object sender, TappedRoutedEventArgs e)
        {
            var result = await joinGrpDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (AddGrpPivot.SelectedIndex == 0)
                {
                    // Create new group
                    if (!(string.IsNullOrEmpty(newGrpName.Text) || string.IsNullOrWhiteSpace(newGrpName.Text))) {
                        Windows.Storage.ApplicationDataContainer container =
                            localSettings.CreateContainer("container", Windows.Storage.ApplicationDataCreateDisposition.Always);
                        Windows.Storage.ApplicationDataContainer keyContainer =
                            localSettings.CreateContainer("keys", Windows.Storage.ApplicationDataCreateDisposition.Always);
                        _ =
                            localSettings.CreateContainer("iv", Windows.Storage.ApplicationDataCreateDisposition.Always);
                        var generatedGUID = Guid.NewGuid().ToString();
                        Aes aes = Aes.Create();
                        localSettings.Containers["container"].Values[generatedGUID] = newGrpName.Text;
                        localSettings.Containers["keys"].Values[generatedGUID] = Convert.ToBase64String(aes.Key, 0, aes.Key.Length);
                        localSettings.Containers["iv"].Values[generatedGUID] = Convert.ToBase64String(aes.IV, 0, aes.IV.Length);
                        NavMain.MenuItems.Add(new muxc.NavigationViewItem
                        {
                            Content = localSettings.Containers["container"].Values[generatedGUID],
                            Icon = new SymbolIcon((Symbol)0xE716),
                            Tag = generatedGUID
                        });
                    }
                }
                else
                {
                    if (joinGrpCode.Text.Length == 6)
                    {
                        if (!localSettings.Values.ContainsKey("RSAPrivate"))
                        {
                            // Get new CSP
                            var csp = new RSACryptoServiceProvider(2048);

                            var privStringWriter = new System.IO.StringWriter();
                            var publicStringWriter = new System.IO.StringWriter();

                            // Serialize Keys
                            new System.Xml.Serialization.XmlSerializer(typeof(RSAParameters))
                                .Serialize(privStringWriter, csp.ExportParameters(true));
                            new System.Xml.Serialization.XmlSerializer(typeof(RSAParameters))
                                .Serialize(publicStringWriter, csp.ExportParameters(false));

                            // Store keys
                            localSettings.Values["RSAPrivate"] = privStringWriter.ToString();
                            localSettings.Values["RSAPublic"] = publicStringWriter.ToString();
                        }

                        var reqGrpUUID = await rootDatabase.Child("invites")
                            .Child(joinGrpCode.Text).OnceSingleAsync<InvitationMgmt>();
                        if (reqGrpUUID != null)
                        {
                            await childObj.Child(reqGrpUUID.GrpUUID).PostAsync(new OutboundMessage 
                            { Author="specialGrpRequest", 
                                Content = localSettings.Values["RSAPublic"].ToString() + "," + joinGrpCode.Text
                            });
                            _ =
                                localSettings.CreateContainer("pendingInvites", Windows.Storage.ApplicationDataCreateDisposition.Always);
                            localSettings.Containers["pendingInvites"].Values[joinGrpCode.Text] = joinGrpCode.Text;

                            await rootDatabase.Child("invites").Child(joinGrpCode.Text).DeleteAsync(); // Each code can only be used once
                        }
                    }
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
                NavMain.SelectedItem = NavMain.MenuItems.ElementAt(0);
                selectedGrpTag = localSettings.Containers["container"].Values.Keys.ToList()[0];
                grpName.Text = localSettings.Containers["container"].Values.Values.ToList()[0].ToString();
            }
        }

        private void grpHeader_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(grpHeader);
        }

        private async void getInviteCode_Click(object sender, RoutedEventArgs e)
        {
            string inviteCode = new Random().Next(0, 1000000).ToString("000000");
            await rootDatabase.Child("invites").Child(inviteCode).PutAsync(new InvitationMgmt{ GrpUUID = selectedGrpTag});
            grpInviteCode.Text = inviteCode;
            grpInviteCode.Visibility = Visibility.Visible;
            getInviteCode.Visibility = Visibility.Collapsed;
        }

        private void Flyout_Closed(object sender, object e)
        {
            grpInviteCode.Visibility = Visibility.Collapsed;
        }

        private void Flyout_Opened(object sender, object e)
        {
            getInviteCode.Visibility = Visibility.Visible;
        }
    }
}
