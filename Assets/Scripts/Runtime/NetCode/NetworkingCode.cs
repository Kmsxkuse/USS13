using System.Collections;
using System.Linq;
using System.Text;
using Unity.Entities;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

namespace Runtime.NetCode
{
    public class NetworkingCode : MonoBehaviour
    {
        public static RTCDataChannel DataChannel;
        private static RTCPeerConnection _peerConnection;
        public GameObject SubtextObject;
        public GameObject ButtonTextObject;
        public GameObject GoBackObject;

        public GameObject HostToggleObject;
        public GameObject HostToggleStartingText;
        public GameObject HostToggleNotice;
        private ButtonStages _buttonStage = ButtonStages.DeterminingHostOrClient;
        private EntityManager _em;
        private string _iceCompressed;
        private Button _networkButton;

        private Text _subtext, _buttonText;

        private void Start()
        {
            _subtext = SubtextObject.GetComponent<Text>();
            _buttonText = ButtonTextObject.GetComponent<Text>();
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _networkButton = GetComponent<Button>();
        }

        private void OnDestroy()
        {
            Debug.LogError("WebRTC disposed.");
            WebRTC.Dispose();
        }

        public void ClickButton()
        {
            // Disabling button.
            _networkButton.interactable = false;

            StartCoroutine(WaitForNetworking());
        }

        private void InitializeWebRtc()
        {
            WebRTC.Initialize();

            var config = new RTCConfiguration
            {
                iceServers = new[]
                {
                    new RTCIceServer
                    {
                        urls = new[] {"stun:stun.l.google.com:19302", "stun:stun1.l.google.com:19302"}
                    }
                }
            };
            _peerConnection = new RTCPeerConnection(ref config);
            _peerConnection.OnIceConnectionChange = state => Debug.LogError("State changed: " + state);
            Debug.LogError("PeerConnection created.");

            var dcInit = new RTCDataChannelInit(true);
            DataChannel = _peerConnection.CreateDataChannel("dataChannel", ref dcInit);
            DataChannel.OnOpen = () => Debug.LogError("Data channel opened.");
            DataChannel.OnClose = () => Debug.LogError("Data channel closed.");

            DataChannel.OnMessage = bytes => Debug.LogError("Data channel received data: " + Encoding.UTF8.GetString(bytes));
            Debug.LogError("Data channel created.");
        }

        private RTCSessionDescriptionAsyncOperation StartClientOffer()
        {
            // Client caller code.
            RTCOfferOptions options = default;
            return _peerConnection.CreateOffer(ref options);
        }

        private IEnumerator WaitForNetworking()
        {
            var textEditor = new TextEditor();
            var timeoutCounter = 0;
            RTCSessionDescription description;

            switch (_buttonStage)
            {
                case ButtonStages.Error: // Error
                    _buttonStage = ButtonStages.Error;
                    _buttonText.text = "ERROR.";
                    _subtext.fontSize = 70;
                    _subtext.text = "Something failed in establishing network connection.\n" +
                                    "Please verify that you had a valid SDP address copied.";
                    break;
                case ButtonStages.DeterminingHostOrClient: // Determining host or client
                    HostToggleStartingText.SetActive(false);
                    HostToggleNotice.SetActive(true);

                    InitializeWebRtc();

                    // True = Host. False = Client.
                    var hostToggle = HostToggleObject.GetComponent<Toggle>().isOn;
                    HostToggleNotice.GetComponent<Text>().text += hostToggle ? "Host." : "Client.";

                    HostToggleObject.SetActive(false);

                    if (hostToggle)
                        goto case ButtonStages.HostPaste;

                    goto case ButtonStages.Client;
                case ButtonStages.Client: // Client
                    _subtext.text = "Please wait. Generating SDP address.";
                    _buttonText.text = "Save SDP address to clipboard.";

                    var clientOffer = StartClientOffer();
                    while (clientOffer.keepWaiting)
                    {
                        // Cant place in function, has goto and a yield return.
                        if (_subtext.text.Count(c => c == '.') > 6)
                        {
                            _subtext.text = "Please wait. Generating SDP address";
                            timeoutCounter++;
                        }

                        if (timeoutCounter > 4)
                            goto case ButtonStages.Error;

                        _subtext.text += ".";
                        yield return new WaitForSeconds(1f);
                    }

                    description = clientOffer.Desc;

                    yield return _peerConnection.SetLocalDescription(ref description);

                    _subtext.text = "Unique SDP protocol address received.\n" +
                                    "Please paste it in Discord for the host to copy.";

                    textEditor.text = JsonUtility.ToJson(description).Compress();
                    textEditor.SelectAll();
                    textEditor.Copy();

                    _buttonStage = ButtonStages.ClientPaste;
                    break;
                case ButtonStages.ClientPaste:
                    GoBackObject.SetActive(false);

                    _subtext.fontSize = 70;
                    _subtext.text = "Please copy unique SDP address from Discord from host.\n" +
                                    "Make sure not to include any whitespace or external characters.";
                    _buttonText.text = "Submit SDP address from clipboard.";
                    _buttonStage = ButtonStages.ClientVerify;
                    break;
                case ButtonStages.ClientVerify:
                    GoBackObject.SetActive(true);

                    _buttonText.text = "Continue.";

                    textEditor.Paste();
                    _iceCompressed = textEditor.text.Trim();

                    _subtext.fontSize = 25;
                    _subtext.text = "Given SDP Address:\n" + _iceCompressed;

                    _buttonStage = ButtonStages.ClientSendToNetworking;
                    break;
                case ButtonStages.ClientSendToNetworking:
                    GoBackObject.SetActive(false);
                    _buttonText.text = "Attempting to connect.";

                    description = JsonUtility.FromJson<RTCSessionDescription>(_iceCompressed.Decompress());
                    yield return _peerConnection.SetRemoteDescription(ref description);

                    _buttonText.text = _peerConnection.ConnectionState.ToString();
                    _buttonStage = ButtonStages.NotReady;
                    break;
                case ButtonStages.HostPaste: // Host
                    GoBackObject.SetActive(false);

                    _subtext.fontSize = 70;
                    _subtext.text = "Please copy unique SDP address from Discord from client.\n" +
                                    "Make sure not to include any whitespace or external characters.";
                    _buttonText.text = "Submit SDP address from clipboard.";
                    _buttonStage = ButtonStages.HostVerify;
                    break;
                case ButtonStages.HostVerify:
                    GoBackObject.SetActive(true);

                    _buttonText.text = "Continue.";

                    textEditor.Paste();
                    _iceCompressed = textEditor.text.Trim();

                    _subtext.fontSize = 25;
                    _subtext.text = "Given SDP Address:\n" + _iceCompressed;

                    _buttonStage = ButtonStages.HostSendToNetworking;
                    break;
                case ButtonStages.HostSendToNetworking: // Host paste SDP address.
                    GoBackObject.SetActive(false);

                    _subtext.fontSize = 70;
                    _subtext.text = "Please wait. Generating SDP address.";
                    _buttonText.text = "Save SDP address to clipboard.";

                    description = JsonUtility.FromJson<RTCSessionDescription>(_iceCompressed.Decompress());
                    yield return _peerConnection.SetRemoteDescription(ref description);

                    RTCAnswerOptions answerOption = default;
                    var hostAnswer = _peerConnection.CreateAnswer(ref answerOption);
                    while (hostAnswer.keepWaiting)
                    {
                        // Cant place in function, has goto and a yield return.
                        if (_subtext.text.Count(c => c == '.') > 6)
                        {
                            _subtext.text = "Please wait. Generating SDP address";
                            timeoutCounter++;
                        }

                        if (timeoutCounter > 4)
                            goto case ButtonStages.Error;

                        _subtext.text += ".";
                        yield return new WaitForSeconds(1f);
                    }

                    _subtext.text = "Unique SDP protocol address received.\n" +
                                    "Please paste it in Discord for the client to copy.";

                    description = hostAnswer.Desc;
                    yield return _peerConnection.SetLocalDescription(ref description);

                    textEditor.text = JsonUtility.ToJson(description).Compress();
                    textEditor.SelectAll();
                    textEditor.Copy();

                    _buttonStage = ButtonStages.NotReady;
                    break;
                case ButtonStages.NotReady:
                    break;
            }

            if (_buttonStage != ButtonStages.Error && _buttonStage != ButtonStages.NotReady)
                _networkButton.interactable = true;
            yield return null;
        }

        public void BackButton()
        {
            switch (_buttonStage)
            {
                case ButtonStages.HostSendToNetworking:
                    _buttonStage = ButtonStages.HostPaste;
                    break;
                case ButtonStages.ClientSendToNetworking:
                    _buttonStage = ButtonStages.ClientPaste;
                    break;
            }

            StartCoroutine(WaitForNetworking());
        }

        private enum ButtonStages
        {
            Error,
            DeterminingHostOrClient,
            Client,
            ClientPaste,
            ClientVerify,
            ClientSendToNetworking,
            HostPaste,
            HostVerify,
            HostSendToNetworking,
            NotReady
        }
    }
}