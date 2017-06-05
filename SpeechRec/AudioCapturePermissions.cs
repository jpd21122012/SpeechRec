using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Capture;

namespace SpeechRec
{
    public class AudioCapturePermissions
    {
        private static int NoCaptureDevicesHResult = -1072845856;
        public async static Task<bool> RequestMicrophonePermission()
        {
            try
            {
                // Request access to the microphone only, to limit the number of capabilities we need
                // to request in the package manifest.
                MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
                settings.StreamingCaptureMode = StreamingCaptureMode.Audio;
                settings.MediaCategory = MediaCategory.Speech;
                MediaCapture capture = new MediaCapture();

                await capture.InitializeAsync(settings);
            }
            catch (TypeLoadException)
            {
                // On SKUs without media player (eg, the N SKUs), we may not have access to the Windows.Media.Capture
                // namespace unless the media player pack is installed. Handle this gracefully.
                var messageDialog = new Windows.UI.Popups.MessageDialog("Media player components are unavailable.");
                await messageDialog.ShowAsync();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // The user has turned off access to the microphone. If this occurs, we should show an error, or disable
                // functionality within the app to ensure that further exceptions aren't generated when 
                // recognition is attempted.
                return false;
            }
            catch (Exception exception)
            {
                // This can be replicated by using remote desktop to a system, but not redirecting the microphone input.
                // Can also occur if using the virtual machine console tool to access a VM instead of using remote desktop.
                if (exception.HResult == NoCaptureDevicesHResult)
                {
                    var messageDialog = new Windows.UI.Popups.MessageDialog("No Audio Capture devices are present on this system.");
                    await messageDialog.ShowAsync();
                    return false;
                }
                else
                {
                    throw;
                }
            }
            return true;
        }
    }
}
