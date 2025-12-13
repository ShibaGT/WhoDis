using BepInEx;
using GorillaLocomotion;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using Valve.Newtonsoft.Json;

namespace WhoDis
{
    public class Tab
    {
        public string tabName;
        public string tabImage;
        public Action callMethod;
        public GameObject Gobject;
        public Tab(string name, string image, Action method)
        {
            tabName = name;
            tabImage = image;
            callMethod = method;
        }
    }

    public class TabButton : MonoBehaviour
    {
        public Tab tab;
        public void Update()
        {
            if (Time.time > Plugin.btnDelay + 1 && (Vector3.Distance(Plugin.rightController.position, tab.Gobject.transform.position) <= 0.15) || Vector3.Distance(Plugin.leftController.position, tab.Gobject.transform.position) <= 0.15)
            {
                Plugin.btnDelay = Time.time;
                tab.callMethod.Invoke();
                GorillaTagger.Instance.StartVibration(false, 1f, 0.1f);
                GorillaTagger.Instance.offlineVRRig.PlayHandTapLocal(84, false, 0.8f);
            }
        }
    }

    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        // made with love by tai/shibagt <3

        public static GameObject mainAsset, mainPanel, mainTabs, playerTabsObj, playerSelectionCapsule;
        public static float btnDelay;
        public static Transform leftController
        { get { return GTPlayer.Instance.LeftHand.controllerTransform; } }
        public static Transform rightController
        { get { return GTPlayer.Instance.RightHand.controllerTransform; } }

        public static Color selectionColor = new Color(0.00f, 0.66f, 0.60f, 0.60f);
        public static Color panelColor = new Color32(5, 5, 10, 250);
        public static Color tabsColor = new Color32(30, 136, 229, 220);

        public static LineRenderer lr;
        public static VRRig selectedPlayer;

        public static int selectedTabIndex = 0;

        public static Tab[] normalTabs = new Tab[]
        {
            new Tab("Home", "home.png", ()=> selectedTabIndex = 0), //0
            new Tab("Players", "players.png", ()=> { selectedTabIndex = 1; showPanel(); }), //1
        };

        public static Tab[] playerTabs = new Tab[]
        {
            new Tab("Player Info", "player.png", ()=> selectedTabIndex = 2), //2
            new Tab("Mods", "mods.png", ()=> selectedTabIndex = 3), //3
        };

        void Update()
        {
            if (ControllerInputPoller.instance.leftControllerGripFloat > 0.5f)
                pointerCast();
            else
                destroyPointer();

            if (ControllerInputPoller.instance.rightControllerPrimaryButton || ControllerInputPoller.instance.leftControllerPrimaryButton)
                if (mainPanel == null)
                    showPanel();

            if (mainPanel != null)
            {
                createText();
                mainAsset.transform.position = GTPlayer.Instance.headCollider.transform.position + GTPlayer.Instance.headCollider.transform.forward * 0.8f;
                mainAsset.transform.rotation = Quaternion.LookRotation(mainAsset.transform.position - GTPlayer.Instance.headCollider.transform.position);
            }
        }

        #region pointer
        void pointerCast()
        {
            Ray ray = new Ray(leftController.position, leftController.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                if (lr == null)
                {
                    var lrob = new GameObject("gunLine");
                    lr = lrob.AddComponent<LineRenderer>();
                    lr.endWidth = 0.01f;
                    lr.startWidth = 0.01f;
                    lr.material.shader = Shader.Find("GUI/Text Shader");
                    lr.material.color = selectionColor;
                }
                lr.SetPosition(0, leftController.position);
                lr.SetPosition(1, hit.point);

                var hitObject = hit.collider.gameObject;
                var player = hitObject.GetComponentInParent<VRRig>();
                if (player != null)
                {
                    // highlight player
                    if (playerSelectionCapsule == null)
                    {
                        playerSelectionCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        Destroy(playerSelectionCapsule.GetComponent<Collider>());
                        var capsuleMat = new Material(Shader.Find("GUI/Text Shader"));
                        capsuleMat.color = selectionColor;
                        playerSelectionCapsule.GetComponent<MeshRenderer>().material = capsuleMat;
                        playerSelectionCapsule.transform.localScale = new Vector3(0.3f, 0.4f, 0.3f);
                    }
                    else
                    {
                        playerSelectionCapsule.transform.position = player.headConstraint.position;
                        playerSelectionCapsule.transform.rotation = player.transform.rotation;
                    }

                    // select player
                    if (ControllerInputPoller.instance.leftControllerIndexFloat > 0.5f)
                        selectPlayer(player);
                }
                else
                {
                    if (playerSelectionCapsule != null)
                    {
                        Destroy(playerSelectionCapsule);
                        playerSelectionCapsule = null;
                    }
                }
            }
        }
        void destroyPointer()
        {
            if (lr != null)
            {
                Destroy(lr.gameObject);
                lr = null;
            }
            if (playerSelectionCapsule != null)
            {
                Destroy(playerSelectionCapsule);
                playerSelectionCapsule = null;
            }
        }
        #endregion

        #region panel

        void selectPlayer(VRRig player) =>
            showPanel(player);

        static void showPanel()
        {
            Debug.Log("drawn");
            destroyPanel();

            if (mainPanel == null)
            {
                mainAsset = LoadAsset("whodis");
                mainPanel = mainAsset.transform.Find("mainPanel").gameObject;
            }

            makeButtons();
        }

        void showPanel(VRRig player)
        {
            selectedPlayer = player;
            selectedTabIndex = 2; //players tab
            showPanel();
        }

        static void destroyPanel()
        {
            if (mainAsset != null)
            {
                Destroy(mainPanel);
                mainPanel = null;

                Destroy(mainAsset);
                mainAsset = null;
                mainTabs = null;
                playerTabsObj = null;

                selectedPlayer = null;
            }
        }

        static void colorPanel()
        {
            if (mainPanel != null)
            {
                mainPanel.GetComponent<Renderer>().material.color = panelColor;
                var line = mainPanel.transform.Find("line").GetComponent<Renderer>();
                line.material.color = tabsColor;
            }
        }

        #endregion

        #region buttons & text

        static void makeButtons()
        {
            // parenting

            mainTabs = mainAsset.transform.Find("mainPanel/mainTabs").gameObject;
            playerTabsObj = mainAsset.transform.Find("mainPanel/playerTabs").gameObject;
            if (selectedPlayer == null)
                playerTabsObj.SetActive(false);

            if (buttonsParent == null)
                buttonsParent = mainAsset.transform.Find("mainPanel/buttons").gameObject;

            PlayersList();

            foreach (Tab t in normalTabs)
            {
                GameObject tabButton = UnityEngine.Object.Instantiate(mainTabs.transform.Find("tab").gameObject, mainTabs.transform);
                tabButton.transform.localPosition = new Vector3(-0.2f + normalTabs.ToList().IndexOf(t) * 0.14f, -0.5012f, -0.004f);
                tabButton.GetComponent<Renderer>().material.color = tabsColor;

                t.Gobject = tabButton;
                tabButton.AddComponent<TabButton>().tab = t;
                GameObject image = tabButton.transform.Find("image").gameObject;
                image.GetComponent<Renderer>().material.shader = Shader.Find("Universal Render Pipeline/Lit");
                image.GetComponent<Renderer>().material.color = Color.white;
                image.GetComponent<Renderer>().material.mainTexture = DownloadImage("https://untitled.rip/menuAssets/" + t.tabImage);
            }
            mainTabs.transform.Find("tab").gameObject.Destroy();

            foreach (Tab t in playerTabs)
            {
                GameObject tabButton = UnityEngine.Object.Instantiate(playerTabsObj.transform.Find("tab").gameObject, playerTabsObj.transform);
                tabButton.transform.localPosition = new Vector3(-0.347f, 0.2964f - playerTabs.ToList().IndexOf(t) * 0.14f, -0.004f);
                tabButton.GetComponent<Renderer>().material.color = tabsColor;

                t.Gobject = tabButton;
                tabButton.AddComponent<TabButton>().tab = t;
                GameObject image = tabButton.transform.Find("image").gameObject;
                image.GetComponent<Renderer>().material.shader = Shader.Find("Universal Render Pipeline/Lit");
                image.GetComponent<Renderer>().material.color = Color.white;
                image.GetComponent<Renderer>().material.mainTexture = DownloadImage("https://untitled.rip/menuAssets/" + t.tabImage);
            }
            playerTabsObj.transform.Find("tab").gameObject.Destroy();

            var stopButton = mainPanel.transform.Find("quit");
            stopButton.GetComponent<Renderer>().material.color = tabsColor;
            var quitTab = new Tab("quit", "quit.png", () => destroyPanel());
            quitTab.Gobject = stopButton.gameObject;
            stopButton.AddComponent<TabButton>().tab = quitTab;
            GameObject quitimage = stopButton.transform.Find("image").gameObject;
            quitimage.GetComponent<Renderer>().material.shader = Shader.Find("Universal Render Pipeline/Lit");
            quitimage.GetComponent<Renderer>().material.color = Color.white;
            quitimage.GetComponent<Renderer>().material.mainTexture = DownloadImage("https://untitled.rip/menuAssets/" + quitTab.tabImage);
            
            colorPanel();
        }

        void createText()
        {
            if (mainPanel == null)
                return;
            var mainText = mainAsset.transform.Find("mainPanel/Canvas/maintext").GetComponent<TextMeshProUGUI>();
            var selectedText = mainAsset.transform.Find("mainPanel/Canvas/selected").GetComponent<TextMeshProUGUI>();
            mainText.fontSize = 6;
            selectedText.text = "by shibagt <3";

            if (selectedTabIndex == 0) //home
                mainText.text = "Welcome to WhoDis!\r\n\r\nSimply hold down your\r\nleft grip to select the\r\nplayer you want to\r\nanalyse!\r\nOr, you can click the\r\nplayers tab below!";
            else if (selectedTabIndex == 1) //players
            {
                var text = "";
                mainText.fontSize = 5;
                foreach (Player p in PhotonNetwork.PlayerListOthers)
                    text += $"{p.NickName}\n";

                mainText.text = !PhotonNetwork.InRoom ? "Not in a room!" : text;
            }
            else if (selectedTabIndex == 2) //player info
            {
                if (selectedPlayer == null)
                {
                    mainText.text = "Selected player left!";
                    return;
                }
                mainText.text =
                        $"{GetPlatform(selectedPlayer)}\n" +
                        $"{GetFPS(selectedPlayer)} FPS\n" +
                        $"{GetPing(selectedPlayer)}ms\n" +
                        $"{GetColor(selectedPlayer)}\n" +
                        $"{GetDate(selectedPlayer)}\n";
                selectedText.text = selectedPlayer.OwningNetPlayer.NickName.ToLower();
            }
            else if (selectedTabIndex == 3) //mods tab
            {
                if (selectedPlayer == null)
                {
                    mainText.text = "Selected player left!";
                    return;
                }
                mainText.fontSize = 5;
                mainText.text =
                    $"Player Mods\n" +
                    $"{GetMods(selectedPlayer)}";
                selectedText.text = selectedPlayer.OwningNetPlayer.NickName.ToLower();
            }
        }

        #endregion

        #region button actions

        static GameObject buttonsParent = null;
        static GameObject tempButton = null;
        static void PlayersList()
        {
            if (selectedTabIndex != 1 || !PhotonNetwork.InRoom)
                return;
            buttonsParent.SetActive(true);
            if (tempButton == null)
                tempButton = buttonsParent.transform.Find("button").gameObject;

            tempButton.SetActive(false);

            float offset = 0f;
            for (int i = PhotonNetwork.PlayerListOthers.Length; i >= 0; i--)
            {
                GameObject playerButton = UnityEngine.Object.Instantiate(tempButton, buttonsParent.transform);
                playerButton.transform.localPosition = new Vector3(0.3005f, -0.4592f - offset, -0.004f);
                playerButton.GetComponent<Renderer>().material.color = tabsColor;
                playerButton.SetActive(true);
                playerButton.AddComponent<TabButton>().tab = new Tab("selectPlayer", "", () =>
                {
                    selectedPlayer = GorillaGameManager.instance.FindPlayerVRRig(PhotonNetwork.PlayerListOthers[i]);
                    selectedTabIndex = 2;
                    showPanel();
                });
                offset += 0.04f;
            }
        }

        #endregion

        #region information methods

        public static AssetBundle assetBundle = null;
        public static GameObject LoadAsset(string assetName)
        {
            GameObject gameObject = null;

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WhoDis.assets." + assetName);
            if (stream != null)
            {
                if (assetBundle == null)
                    assetBundle = AssetBundle.LoadFromStream(stream);
                gameObject = UnityEngine.Object.Instantiate(assetBundle.LoadAsset<GameObject>(assetName));
            }
            else
            {
                Debug.LogError("Failed to load asset from resource: " + assetName);
            }

            return gameObject;
        }

        static Dictionary<string, Texture> imageCache = new Dictionary<string, Texture> { };

        public static Texture DownloadImage(string url)
        {
            try
            {
                if (imageCache.ContainsKey(url))
                    return imageCache[url];

                WebClient webClient = new WebClient();

                byte[] imageData = webClient.DownloadData(url);

                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(imageData);

                Texture convertedTexture = texture;

                webClient.Dispose();

                imageCache.Add(url, convertedTexture);
                return convertedTexture;
            }
            catch (Exception ex)
            {
                Debug.LogError("Error downloading image: " + ex.Message);
                return null;
            }
        }

        public static string GetPlatform(VRRig rig)
        {
            string concatStringOfCosmeticsAllowed = rig.concatStringOfCosmeticsAllowed;

            if (concatStringOfCosmeticsAllowed.Contains("S. FIRST LOGIN"))
                return "<color=blue>Steam</color>";
            else if (concatStringOfCosmeticsAllowed.Contains("FIRST LOGIN") || rig.Creator.GetPlayerRef().CustomProperties.Count >= 2)
                return "<color=blue>PC</color>";

            return "<color=green>Quest</color>";
        }

        public static string GetFPS(VRRig rig)
        {
            var fps = rig.fps;
            string fpsString = fps.ToString();

            if (fps < 30)
                fpsString = $"<color=red>{fps.ToString()}</color>";
            if (fps > 30 && fps < 60)
                fpsString = $"<color=yellow>{fps.ToString()}</color>";
            if (fps > 60)
                fpsString = $"<color=green>{fps.ToString()}</color>";
            if (fps > 100)
                fpsString = $"<color=cyan>{fps.ToString()}</color>";

            return fpsString;
        }

        public static string GetPing(VRRig rig)
        {
            double ping = Math.Abs((rig.velocityHistoryList[0].time - PhotonNetwork.Time) * 1000);
            int safePing = (int)Math.Clamp(Math.Round(ping), 0, int.MaxValue);

            return safePing.ToString();
        }

        public static string GetColor(VRRig rig)
        {
            var input = rig.playerColor;
            return $"<color=red>{Mathf.Round(input.r * 10)}</color> <color=green>{Mathf.Round(input.g * 10)}</color> <color=blue>{Mathf.Round(input.b * 10)}</color>";
        }

        static Dictionary<string, string> datePool = new Dictionary<string, string> { };
        private static string GetDate(VRRig rig)
        {
            string UserId = rig.Creator.UserId;

            if (datePool.ContainsKey(UserId))
                return datePool[UserId];
            else
            {
                datePool.Add(UserId, "LOADING");
                PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest { PlayFabId = UserId }, delegate (GetAccountInfoResult result)
                {
                    string date = result.AccountInfo.Created.ToString("MMM dd, yyyy HH:mm").ToUpper();
                    datePool[UserId] = date;
                    rig.UpdateName();
                }, delegate { datePool[UserId] = "ERROR"; rig.UpdateName(); }, null, null);
                return "LOADING";
            }
        }

        public IEnumerator CheckifUser(string uid, string action, Action success, Action failure)
        {
            UnityWebRequest request = new UnityWebRequest($"https://iidk.online/{action}", "POST");

            string json = JsonConvert.SerializeObject(new { uid });

            byte[] raw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(raw);
            request.SetRequestHeader("Content-Type", "application/json");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                success.Invoke();
            else
                failure.Invoke();
        }

        bool isUsingiiDk(VRRig rig)
        {
            bool isUsingiiDk = false;
            StartCoroutine(CheckifUser(rig.OwningNetPlayer.UserId, "frienduser",
                () => isUsingiiDk = true,
                () => isUsingiiDk = false
            ));
            return isUsingiiDk;
        }

        string GetMods(VRRig rig)
        {
            string specialMods = "";
            NetPlayer creator = rig.Creator;

            Dictionary<string, string[]> specialModsList = new Dictionary<string, string[]> {
                { "genesis", new string[] { "GENESIS", "07019C" } },
                { "HP_Left", new string[] { "HOLDABLEPAD", "332316" } },
                { "GrateVersion", new string[] { "GRATE", "707070" } },
                { "void", new string[] { "VOID", "FFFFFF" } },
                { "BANANAOS", new string[] { "BANANAOS", "FFFF00" } },
                { "GC", new string[] { "GORILLACRAFT", "43B581" } },
                { "CarName", new string[] { "GORILLAVEHICLES", "43B581" } },
                { "6p72ly3j85pau2g9mda6ib8px", new string[] { "CCMV2", "BF00FC" } },
                { "FPS-Nametags for Zlothy", new string[] { "FPSTAGS", "B103FC" } },
                { "cronos", new string[] { "CRONOS", "0000FF" } },
                { "ORBIT", new string[] { "ORBIT", "FFFFFF" } },
                { "Violet On Top", new string[] { "VIOLET", "DF6BFF" } },
                { "MP25", new string[] { "MONKEPHONE", "707070" } },
                { "GorillaWatch", new string[] { "GORILLAWATCH", "707070" } },
                { "InfoWatch", new string[] { "GORILLAINFOWATCH", "707070" } },
                { "BananaPhone", new string[] { "BANANAPHONE", "FFFC45" } },
                { "Vivid", new string[] { "VIVID", "F000BC" } },
                { "RGBA", new string[] { "CUSTOMCOSMETICS", "FF0000" } },
                { "cheese is gouda", new string[] { "WHOSICHEATING", "707070" } },
                { "shirtversion", new string[] { "GORILLASHIRTS", "707070" } },
                { "gpronouns", new string[] { "GORILLAPRONOUNS", "707070" } },
                { "gfaces", new string[] { "GORILLAFACES", "707070" } },
                { "monkephone", new string[] { "MONKEPHONE", "707070" } },
                { "pmversion", new string[] { "PLAYERMODELS", "707070" } },
                { "gtrials", new string[] { "GORILLATRIALS", "707070" } },
                { "msp", new string[] { "MONKESMARTPHONE", "707070" } },
                { "gorillastats", new string[] { "GORILLASTATS", "707070" } },
                { "using gorilladrift", new string[] { "GORILLADRIFT", "707070" } },
                { "monkehavocversion", new string[] { "MONKEHAVOC", "707070" } },
                { "tictactoe", new string[] { "TICTACTOE", "a89232" } },
                { "ccolor", new string[] { "INDEX", "0febff" } },
                { "imposter", new string[] { "GORILLAAMONGUS", "ff0000" } },
                { "spectapeversion", new string[] { "SPECTAPE", "707070" } },
                { "cats", new string[] { "CATS", "707070" } },
                { "made by biotest05 :3", new string[] { "DOGS", "707070" } },
                { "fys cool magic mod", new string[] { "FYSMAGICMOD", "707070" } },
                { "colour", new string[] { "CUSTOMCOSMETICS", "707070" } },
                { "chainedtogether", new string[] { "CHAINED TOGETHER", "707070" } },
                { "goofywalkversion", new string[] { "GOOFYWALK", "707070" } },
                { "void_menu_open", new string[] { "VOID", "303030" } },
                { "violetpaiduser", new string[] { "VIOLETPAID", "DF6BFF" } },
                { "violetfree", new string[] { "VIOLETFREE", "DF6BFF" } },
                { "obsidianmc", new string[] { "OBSIDIAN.LOL", "303030" } },
                { "dark", new string[] { "SHIBAGT DARK", "303030" } },
                { "hidden menu", new string[] { "HIDDEN", "707070" } },
                { "oblivionuser", new string[] { "OBLIVION", "5055d3" } },
                { "hgrehngio889584739_hugb\n", new string[] { "RESURGENCE", "470050" } },
                { "eyerock reborn", new string[] { "EYEROCK", "707070" } },
                { "asteroidlite", new string[] { "ASTEROID LITE", "707070" } },
                { "elux", new string[] { "ELUX", "707070" } },
                { "cokecosmetics", new string[] { "COKE COSMETX", "00ff00" } },
                { "GFaces", new string[] { "gFACES", "707070" } },
                { "github.com/maroon-shadow/SimpleBoards", new string[] { "SIMPLEBOARDS", "707070" } },
                { "ObsidianMC", new string[] { "OBSIDIAN", "DC143C" } },
                { "hgrehngio889584739_hugb", new string[] { "RESURGENCE", "707070" } },
                { "GTrials", new string[] { "gTRIALS", "707070" } },
                { "github.com/ZlothY29IQ/GorillaMediaDisplay", new string[] { "GMD", "B103FC" } },
                { "github.com/ZlothY29IQ/TooMuchInfo", new string[] { "TOOMUCHINFO", "B103FC" } },
                { "github.com/ZlothY29IQ/RoomUtils-IW", new string[] { "ROOMUTILS-IW", "B103FC" } },
                { "github.com/ZlothY29IQ/MonkeClick", new string[] { "MONKECLICK", "B103FC" } },
                { "github.com/ZlothY29IQ/MonkeClick-CI", new string[] { "MONKECLICK-CI", "B103FC" } },
                { "github.com/ZlothY29IQ/MonkeRealism", new string[] { "MONKEREALISM", "B103FC" } },
                { "MediaPad", new string[] { "MEDIAPAD", "B103FC" } },
                { "GorillaCinema", new string[] { "gCINEMA", "B103FC" } },
                { "ChainedTogetherActive", new string[] { "CHAINEDTOGETHER", "B103FC" } },
                { "GPronouns", new string[] { "gPRONOUNS", "707070" } },
                { "CSVersion", new string[] { "CustomSkin", "707070" } },
                { "github.com/ZlothY29IQ/Zloth-RecRoomRig", new string[] { "ZLOTH-RRR", "B103FC" } },
                { "ShirtProperties", new string[] { "SHIRTS-OLD", "707070" } },
                { "GorillaShirts", new string[] { "SHIRTS", "707070" } },
                { "GS", new string[] { "OLD SHIRTS", "707070" } },
                { "6XpyykmrCthKhFeUfkYGxv7xnXpoe2", new string[] { "CCMV2", "DC143C" } },
                { "Body Tracking", new string[] { "BODYTRACK-OLD", "7AA11F" } },
                { "Body Estimation", new string[] { "HANBodyEst", "7AA11F" } },
                { "Gorilla Track", new string[] { "BODYTRACK", "7AA11F" } },
                { "CustomMaterial", new string[] { "CUSTOMCOSMETICS", "707070" } },
                { "I like cheese", new string[] { "RECROOMRIG", "FE8232" } },
                { "silliness", new string[] { "SILLINESS", "FFBAFF" } },
                { "emotewheel", new string[] { "EMOTEWHEEL", "1E2030" } },
                { "untitled", new string[] { "UNTITLED", "2D73AF" } }
            };

            Dictionary<string, object> customProps = new Dictionary<string, object>();
            foreach (DictionaryEntry dictionaryEntry in creator.GetPlayerRef().CustomProperties)
                customProps[dictionaryEntry.Key.ToString().ToLower()] = dictionaryEntry.Value;

            foreach (KeyValuePair<string, string[]> specialMod in specialModsList)
            {
                if (customProps.ContainsKey(specialMod.Key.ToLower()))
                    specialMods += (specialMods == "" ? "" : ", ") + "<color=#" + specialMod.Value[1].ToUpper() + ">" + specialMod.Value[0].ToUpper() + "</color>";
            }

            if (isUsingiiDk(rig))
                specialMods += (specialMods == "" ? "" : ", ") + "<color=orange>iiDK</color>";

            CosmeticsController.CosmeticSet cosmeticSet = rig.cosmeticSet;
            foreach (CosmeticsController.CosmeticItem cosmetic in cosmeticSet.items)
            {
                if (!cosmetic.isNullItem && !rig.concatStringOfCosmeticsAllowed.Contains(cosmetic.itemName))
                {
                    specialMods += (specialMods == "" ? "" : ", ") + "<color=green>COSMETX</color>";
                    break;
                }
            }

            return specialMods == "" ? null : specialMods;
        }

        #endregion
    }
}
