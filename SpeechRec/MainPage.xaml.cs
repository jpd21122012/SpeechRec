using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SpeechRec
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private SpeechRecognizer speechRecognizer;
        private bool isListening;
        private StringBuilder dictatedTextBuilder;
        private static uint HResultPrivacyStatementDeclined = 0x80045509;

        public MainPage()
        {
            this.InitializeComponent();
            isListening = false;
            dictatedTextBuilder = new StringBuilder();
        }
        /// <summary>
        /// Look up the supported languages for this speech recognition scenario, 
        /// that are installed on this machine, and populate a dropdown with a list.
        /// </summary>
        private void PopulateLanguageDropdown()
        {
            Language defaultLanguage = SpeechRecognizer.SystemSpeechLanguage;
            IEnumerable<Language> supportedLanguages = SpeechRecognizer.SupportedTopicLanguages;
            foreach (Language lang in supportedLanguages)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Tag = lang;
                item.Content = lang.DisplayName;

                cbLanguageSelection.Items.Add(item);
                if (lang.LanguageTag == defaultLanguage.LanguageTag)
                {
                    item.IsSelected = true;
                    cbLanguageSelection.SelectedItem = item;
                }
            }
        }
        private async void cbLanguageSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (speechRecognizer != null)
            {
                ComboBoxItem item = (ComboBoxItem)(cbLanguageSelection.SelectedItem);
                Language newLanguage = (Language)item.Tag;
                if (speechRecognizer.CurrentLanguage != newLanguage)
                {
                    // trigger cleanup and re-initialization of speech.
                    try
                    {
                        await InitializeRecognizer(newLanguage);
                    }
                    catch (Exception exception)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(exception.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
        }
        private async Task InitializeRecognizer(Language recognizerLanguage)
        {
            if (speechRecognizer != null)
            {
                // cleanup prior to re-initializing this scenario.
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;
                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
                speechRecognizer.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }

            this.speechRecognizer = new SpeechRecognizer(recognizerLanguage);

            // Provide feedback to the user about the state of the recognizer. This can be used to provide visual feedback in the form
            // of an audio indicator to help the user understand whether they're being heard.
            speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;

            // Apply the dictation topic constraint to optimize for dictated freeform speech.
            var dictationConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation");
            speechRecognizer.Constraints.Add(dictationConstraint);
            SpeechRecognitionCompilationResult result = await speechRecognizer.CompileConstraintsAsync();
            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                btnContinuousRecognize.IsEnabled = false;
            }
            speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
            speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            speechRecognizer.HypothesisGenerated += SpeechRecognizer_HypothesisGenerated;
        }
        private async void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            if (args.Status != SpeechRecognitionResultStatus.Success)
            {
                // If TimeoutExceeded occurs, the user has been silent for too long. We can use this to 
                // cancel recognition if the user in dictation mode and walks away from their device, etc.
                // In a global-command type scenario, this timeout won't apply automatically.
                // With dictation (no grammar in place) modes, the default timeout is 20 seconds.
                if (args.Status == SpeechRecognitionResultStatus.TimeoutExceeded)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        DictationButtonText.Text = " Dictate";
                        cbLanguageSelection.IsEnabled = true;
                        dictationTextBox.Text = dictatedTextBuilder.ToString();
                        isListening = false;
                    });
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        DictationButtonText.Text = " Dictate";
                        cbLanguageSelection.IsEnabled = true;
                        isListening = false;
                    });
                }
            }
        }
        private async void SpeechRecognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            string hypothesis = args.Hypothesis.Text;

            // Update the textbox with the currently confirmed text, and the hypothesis combined.
            string textboxContent = dictatedTextBuilder.ToString() + " " + hypothesis + " ...";
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                dictationTextBox.Text = textboxContent;
                btnClearText.IsEnabled = true;
            });
        }
        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // We may choose to discard content that has low confidence, as that could indicate that we're picking up
            // noise via the microphone, or someone could be talking out of earshot.
            string command = "";
            if (args.Result.Confidence == SpeechRecognitionConfidence.Medium ||
                args.Result.Confidence == SpeechRecognitionConfidence.High)
            {
                dictatedTextBuilder.Append(args.Result.Text + " ");

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    discardedTextBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                    dictationTextBox.Text = dictatedTextBuilder.ToString();
                    btnClearText.IsEnabled = true;
                    command = dictationTextBox.Text;
                    Task.Delay(TimeSpan.FromSeconds(3));
                    System.Diagnostics.Debug.WriteLine("Comando: " + command.Trim(' ').Trim('.'));
                    dictatedTextBuilder.Clear();
                    dictationTextBox.Text = "";
                });
                if (command.Trim(' ').Trim('.') == "12345")
                {
                    System.Diagnostics.Debug.WriteLine("Dijiste el comando 1");
                    command = "";
                }
            }
            else
            {
                // In some scenarios, a developer may choose to ignore giving the user feedback in this case, if speech
                // is not the primary input mechanism for the application.
                // Here, just remove any hypothesis text by resetting it to the last known good.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
               {
                   dictationTextBox.Text = dictatedTextBuilder.ToString();
                   string discardedText = args.Result.Text;

                   if (!string.IsNullOrEmpty(discardedText))
                   {
                       discardedText = discardedText.Length <= 25 ? discardedText : (discardedText.Substring(0, 25) + "...");

                       discardedTextBlock.Text = "Discarded due to low/rejected Confidence: " + discardedText;
                       discardedTextBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
                   }
               });
            }
        }
        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
            });
        }
        public async void ContinuousRecognize_Click(object sender, RoutedEventArgs e)
        {
            btnContinuousRecognize.IsEnabled = false;
            if (isListening == false)
            {
                // The recognizer can only start listening in a continuous fashion if the recognizer is currently idle.
                // This prevents an exception from occurring.
                if (speechRecognizer.State == SpeechRecognizerState.Idle)
                {
                    DictationButtonText.Text = " Stop Dictation";
                    cbLanguageSelection.IsEnabled = false;
                    hlOpenPrivacySettings.Visibility = Visibility.Collapsed;
                    discardedTextBlock.Visibility = Visibility.Collapsed;

                    try
                    {
                        isListening = true;
                        await speechRecognizer.ContinuousRecognitionSession.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        if ((uint)ex.HResult == HResultPrivacyStatementDeclined)
                        {
                            // Show a UI link to the privacy settings.
                            hlOpenPrivacySettings.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                            await messageDialog.ShowAsync();
                        }

                        isListening = false;
                        DictationButtonText.Text = " Dictate";
                        cbLanguageSelection.IsEnabled = true;

                    }
                }
            }
            else
            {
                isListening = false;
                DictationButtonText.Text = " Dictate";
                cbLanguageSelection.IsEnabled = true;

                if (speechRecognizer.State != SpeechRecognizerState.Idle)
                {
                    // Cancelling recognition prevents any currently recognized speech from
                    // generating a ResultGenerated event. StopAsync() will allow the final session to 
                    // complete.
                    try
                    {
                        await speechRecognizer.ContinuousRecognitionSession.StopAsync();

                        // Ensure we don't leave any hypothesis text behind
                        dictationTextBox.Text = dictatedTextBuilder.ToString();
                    }
                    catch (Exception exception)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(exception.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
            btnContinuousRecognize.IsEnabled = true;
        }

        private void btnClearText_Click(object sender, RoutedEventArgs e)
        {
            btnClearText.IsEnabled = false;
            dictatedTextBuilder.Clear();
            dictationTextBox.Text = "";
            discardedTextBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            // Avoid setting focus on the text box, since it's a non-editable control.
            btnContinuousRecognize.Focus(FocusState.Programmatic);
        }
        private void dictationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(dictationTextBox, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer))
                {
                    continue;
                }

                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }
        private async void openPrivacySettings_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-speechtyping"));
        }
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Prompt the user for permission to access the microphone. This request will only happen
            // once, it will not re-prompt if the user rejects the permission.
            bool permissionGained = await AudioCapturePermissions.RequestMicrophonePermission();
            if (permissionGained)
            {
                btnContinuousRecognize.IsEnabled = true;

                PopulateLanguageDropdown();
                await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);
            }
            else
            {
                this.dictationTextBox.Text = "Permission to access capture resources was not given by the user, reset the application setting in Settings->Privacy->Microphone.";
                btnContinuousRecognize.IsEnabled = false;
                cbLanguageSelection.IsEnabled = false;
            }
        }

        private void Page_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ContinuousRecognize_Click(sender, e);
        }
        //protected async override void OnNavigatedFrom(NavigationEventArgs e)
        //{
        //    if (this.speechRecognizer != null)
        //    {
        //        if (isListening)
        //        {
        //            await this.speechRecognizer.ContinuousRecognitionSession.CancelAsync();
        //            isListening = false;
        //            DictationButtonText.Text = " Dictate";
        //            cbLanguageSelection.IsEnabled = true;
        //        }

        //        dictationTextBox.Text = "";

        //        speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
        //        speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;
        //        speechRecognizer.HypothesisGenerated -= SpeechRecognizer_HypothesisGenerated;
        //        speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;

        //        this.speechRecognizer.Dispose();
        //        this.speechRecognizer = null;
        //    }
        //}
    }
}
