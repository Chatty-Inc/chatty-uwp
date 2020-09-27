/*
 * Just in case people come and look at this in the year 2050...
 * 
 * Written by Vincent Kwok (CryptoAlgo) in 2020
 * (c) 2020-2020 
 * 
 * Chatty is distributed under the terms of the GNU General Public License Version 3.
 * 
 *   This program is free software: you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation, either version 3 of the License, or
 *   any later version.
 *  
 *   This program is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU General Public License for more details.
 *  
 *   You should have received a copy of the GNU General Public License
 *   along with this program.  If not, see <https://www.gnu.org/licenses/>.
 * 
 * CryptoAlgo Inc., hereby disclaims all copyright interest in the program “Chatty” 
 * (which allows secure online communication between 2 or more people) written by Vincent Kwok.
 * 
 * Vincent Kwok, 26 September 2020
 * Owner of CryptoAlgo
 */


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
using yt=YouTubeSearch;
using System.Text.RegularExpressions;
using Chatty;
using Windows.Networking.Connectivity;
using System.Net.NetworkInformation;

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
        public string GrpUUID { get; set; }
    }

    public class UpdateChecker
    {
        public string currentVer { get; set; }
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

        bool registeredNetworkStatusNotif = false;

        private async void ShowTitleBarText(string text, byte r, byte g, byte b)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                titleBarText.Text = text;
                Task.Delay(4000);
                titleBarText.Text = "Chatty - " + text;
                topTitleBar.Background = new SolidColorBrush(Color.FromArgb(50, r, g, b));
            });
        }

        private async void ShowUpdatePrompt(string infoText, bool disableMsgBox)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                updatePrompt.Hide();
                if (disableMsgBox) msgTextbox.IsEnabled = false;
                else msgTextbox.IsEnabled = true;
                updateText.Text = infoText;
                var result = updatePrompt.ShowAsync();
            });
        }

        private async void UpdateVerChecker(string newVer)
        {
            var versionCode = newVer.Split('.'); // MainVer = 0, SubVer = 1, MinorVer = 2 (0.0.0)
            int[] currentVer = { 0, 6, 8 }; // CHANGE THIS!!!

            if (int.Parse(versionCode[0]) > currentVer[0])
            {
                // Kill apps
                ShowUpdatePrompt("This version of Chatty (" + string.Join('.', currentVer) + ") is obselete." + Environment.NewLine + 
                    "Sending of messages has been disabled." + Environment.NewLine + "Please update to the latest version (" + newVer + ")" + 
                    Environment.NewLine + "to enjoy the latest Chatty features",
                    true);
            }
            else if (int.Parse(versionCode[1]) - 1 > currentVer[1])
            {
                // Show warning
                ShowUpdatePrompt("Please update to the latest Chatty version (" + newVer + ")" + Environment.NewLine +
                    "This version (" + string.Join('.', currentVer) + ") will soon reach EOL" + Environment.NewLine +
                    "When EOL is reached, no messages can be sent", false);
            }
            else if (int.Parse(versionCode[2]) - 2 > currentVer[2])
            {
                // Show info
                ShowUpdatePrompt("A new version of Chatty is avaliable." + Environment.NewLine +
                    "Please update soon to enjoy the latest features" + Environment.NewLine + 
                    "and stability improvements", false);
            }
        }

        public MainPage()
        {
            this.InitializeComponent();

            rootDatabase = new FirebaseClient("https://chatty-inc.firebaseio.com/",
                new FirebaseOptions
                {
                    OfflineDatabaseFactory = (t, s) => new OfflineDatabase(t, s),
                    AuthTokenAsyncFactory = () => Task.FromResult("PJO0RNg8OciCbRgajSf2U2UKbFa6Tkno7oo3acqy")
                });
            childObj = rootDatabase.Child("messages");

            // Self destruction feature to ensure older versions are not being used
            rootDatabase.Child("versions").AsObservable<UpdateChecker>()
                .Where(f => !string.IsNullOrEmpty(f.Key))
                .Subscribe(f => UpdateVerChecker(f.Object.currentVer));

            rootDatabase.Child("encryptedAESKeys").AsObservable<AcceptedInvitation>()
                .Where(f => !string.IsNullOrEmpty(f.Key))
                .Subscribe(f => reqJoinResp(f.Object, f.Key));

            // Register for network status change notifications to change title bar color
            var networkStatusCallback = new NetworkStatusChangedEventHandler(OnNetworkStatusChange);
            if (!registeredNetworkStatusNotif)
            {
                NetworkInformation.NetworkStatusChanged += networkStatusCallback;
                registeredNetworkStatusNotif = true;
            }

            if (NetworkInterface.GetIsNetworkAvailable())
            {
                ShowTitleBarText("Connected", 0, 204, 168);
            }
            else
            {
                ShowTitleBarText("No connection", 232, 17, 35);
            }

            async void OnNetworkStatusChange(object sender)
            {
                // get the ConnectionProfile that is currently used to connect to the Internet                
                ConnectionProfile InternetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();

                if (InternetConnectionProfile == null)
                {
                    ShowTitleBarText("No connection", 232, 17, 35);
                }
                else
                {
                    ShowTitleBarText("Connected", 0, 204, 168);
                }
            }
        }


        private async void reqJoinResp(AcceptedInvitation invitationResp, string key)
        {
            if (!localSettings.Containers.ContainsKey("pendingInvites")) return;
            if (!localSettings.Containers["pendingInvites"].Values.ContainsKey(key)) return;
            if (!invitationResp.EncryptedAES.Equals("rej")) { // Invitation accepted
                addNavGrp(invitationResp.GrpName, invitationResp.GrpUUID);
                _ =
                    localSettings.CreateContainer("container", Windows.Storage.ApplicationDataCreateDisposition.Always);
                _ =
                    localSettings.CreateContainer("keys", Windows.Storage.ApplicationDataCreateDisposition.Always);
                _ =
                    localSettings.CreateContainer("iv", Windows.Storage.ApplicationDataCreateDisposition.Always);

                // Get the object back from the stream
                var privKey = (RSAParameters)new System.Xml.Serialization.XmlSerializer(typeof(RSAParameters)).Deserialize(new System.IO.StringReader(localSettings.Values["RSAPrivate"].ToString()));

                var bytesCypherText = Convert.FromBase64String(invitationResp.EncryptedAES);
                var csp = new RSACryptoServiceProvider();
                csp.ImportParameters(privKey);

                // Get back original encrypted text
                var plainTextData = System.Text.Encoding.Unicode.GetString(csp.Decrypt(bytesCypherText, false));

                var aesKeys = plainTextData.Split(',');

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    localSettings.Containers["container"].Values[invitationResp.GrpUUID] = invitationResp.GrpName;
                    localSettings.Containers["keys"].Values[invitationResp.GrpUUID] = aesKeys[0];
                    localSettings.Containers["iv"].Values[invitationResp.GrpUUID] = aesKeys[1];
                });
            }
            localSettings.Containers["pendingInvites"].Values.Remove(key); // Delete waiting key from localSettings
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
                sendLoading.IsIndeterminate = true;
                sendLoading.Visibility = Visibility.Visible;
            }
            else
            {
                sendLoading.IsIndeterminate = false;
                sendLoading.Visibility = Visibility.Collapsed;
            }
        }

        DateTime messageLastSent = DateTime.UtcNow;

        string prevMsg = "";

        private async void MsgTextbox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key.Equals(Windows.System.VirtualKey.Enter))
            {
                var msg = msgTextbox.Text;
                if (msg.Contains(prevMsg))
                {
                    suspicion += 10;
                }

                prevMsg = msg;

                if (messageLastSent.AddMilliseconds(Math.Abs(msg.Length-20) * 200) > DateTime.UtcNow)
                {
                    if (msg.Length < 5)
                    {
                        suspicion += Math.Abs(msg.Length - 5);
                    }
                    else
                    {
                        suspicion += msg.Length / 100;
                    }
                }
                messageLastSent = DateTime.UtcNow;

                if (string.IsNullOrEmpty(msg) || string.IsNullOrWhiteSpace(msg))
                {
                    msgBoxFlyoutText.Text = "Your message is empty";
                    FlyoutBase.ShowAttachedFlyout(msgTextbox);
                    return;
                }
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
                        await Task.Delay(1000); // I don't want to wait the whole day to text my app
                    }

                    msgTextbox.Header = null;
                    msgTextbox.IsEnabled = true;

                    suspicion += brokeRulesTimes;
                }
                else
                {
                    ShowHideLoader();
                    try
                    {
                        await childObj.Child(selectedGrpTag).PostAsync(new OutboundMessage
                        {
                            Author = uuid,
                            Content =
                            EncryptMsg(msg, localSettings.Containers["keys"].Values[selectedGrpTag].ToString(), localSettings.Containers["iv"].Values[selectedGrpTag].ToString())
                        });
                        msgTextbox.Text = ""; // Clear messagebox
                    }
                    catch (Firebase.Database.FirebaseException)
                    {
                        msgBoxFlyoutText.Text = "Failed to send message" + Environment.NewLine + "Please check your network connection";
                        FlyoutBase.ShowAttachedFlyout(msgTextbox);
                    }
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
        private object sendMsgLoader;

        private void Messages_Loaded(object sender, RoutedEventArgs e)
        {
            //var observableDatabase = childObj.AsRealtimeDatabase<InboundMessage>("", "", StreamingOptions.LatestOnly, InitialPullStrategy.Everything, true)
            //    .AsObservable();

            if (!reloadedListOnce)
            {
                reloadList();
                reloadedListOnce = true;
            }
        }

        private async void AddListInvite(string rawInput)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
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
                    Tag = rawInput,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                acceptInvite.Click += AcceptButton_Click;
                Button rejectInvite = new Button
                {
                    Content = "Deny Request",
                    Tag = rawInput,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                rejectInvite.Click += RejectInvite_Click;

                buttonHolder.Children.Add(acceptInvite);
                buttonHolder.Children.Add(rejectInvite);
                inviteHolder.Children.Add(reqText);
                inviteHolder.Children.Add(buttonHolder);
                item.Content = inviteHolder;
                messages.Items.Add(item);
            });
        }

        private async void deleteChildren(string childTag)
        {
            await rootDatabase.Child("encryptedAESKeys").Child(childTag).DeleteAsync();
        }

        private async void RejectInvite_Click(object sender, RoutedEventArgs e)
        {
            var rejectButton = (Button)sender;
            var arrayParams = rejectButton.Tag.ToString().Split(',');
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                rootDatabase.Child("encryptedAESKeys").Child(arrayParams[1])
                .PutAsync(new AcceptedInvitation
                {
                    EncryptedAES = "rej", // Rejected request
                    GrpName = grpName.Text,
                    GrpUUID = selectedGrpTag
                });
            });


        }

        private async void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            var acceptedButton = (Button)sender;
            var arrayParams = acceptedButton.Tag.ToString().Split(',');
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
                rootDatabase.Child("encryptedAESKeys").Child(arrayParams[1])
                .PutAsync(new AcceptedInvitation
                {
                    EncryptedAES = cypherText,
                    GrpName = grpName.Text,
                    GrpUUID = selectedGrpTag
                });
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

                holder.Children.Add(usertext);
                item.Content = holder;
                messages.Items.Add(item);
            });
        }

        private async void NavigationView_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            msgTextbox.IsEnabled = true;
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

        private async void addNavGrp(string content, string tag)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                NavMain.MenuItems.Add(new muxc.NavigationViewItem
                {
                    Content = content,
                    Icon = new SymbolIcon((Symbol)0xE716),
                    Tag = tag
                });
            });
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
                        _ =
                            localSettings.CreateContainer("container", Windows.Storage.ApplicationDataCreateDisposition.Always);
                        _ =
                            localSettings.CreateContainer("keys", Windows.Storage.ApplicationDataCreateDisposition.Always);
                        _ =
                            localSettings.CreateContainer("iv", Windows.Storage.ApplicationDataCreateDisposition.Always);
                        var generatedGUID = Guid.NewGuid().ToString();
                        Aes aes = Aes.Create();
                        localSettings.Containers["container"].Values[generatedGUID] = newGrpName.Text;
                        localSettings.Containers["keys"].Values[generatedGUID] = Convert.ToBase64String(aes.Key, 0, aes.Key.Length);
                        localSettings.Containers["iv"].Values[generatedGUID] = Convert.ToBase64String(aes.IV, 0, aes.IV.Length);
                        addNavGrp((string)localSettings.Containers["container"].Values[generatedGUID], generatedGUID);
                    }
                }
                else
                {
                    if (joinGrpCode.Text.Length == 6)
                    {
                        var reqGrpUUID = await rootDatabase.Child("invites")
                            .Child(joinGrpCode.Text).OnceSingleAsync<InvitationMgmt>();
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

                        if (reqGrpUUID != null)
                        {
                            await childObj.Child(reqGrpUUID.GrpUUID).PostAsync(new OutboundMessage 
                            { 
                                Author="specialGrpRequest", 
                                Content = localSettings.Values["RSAPublic"].ToString() + "," + joinGrpCode.Text
                            });

                            _ = localSettings.CreateContainer("pendingInvites", Windows.Storage.ApplicationDataCreateDisposition.Always);
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
                    addNavGrp((string)singleObj.Value, singleObj.Key);
                }
                if (localSettings.Containers["container"].Values.ToList().Count == 0)
                {
                    ListViewItem item = new ListViewItem();
                    TextBlock message = new TextBlock
                    {
                        Text = "Add or create your first group by clicking on the add icon in the left menu",
                        TextWrapping = TextWrapping.Wrap
                    };
                    item.Content = message;
                    messages.Items.Add(item);
                    msgTextbox.IsEnabled = false;
                }
                else
                {
                    // NavMain.SelectedItem = NavMain.MenuItems.ElementAt(0);
                    selectedGrpTag = localSettings.Containers["container"].Values.Keys.ToList()[0];
                    grpName.Text = localSettings.Containers["container"].Values.Values.ToList()[0].ToString();
                }
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
