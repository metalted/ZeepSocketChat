using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using WebSocketSharp;
using WebSocketSharp.Server;
using UnityEngine;

namespace ZeepSocket
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string pluginGUID = "com.metalted.zeepkist.zeepsocket";
        public const string pluginName = "ZeepSocket";
        public const string pluginVersion = "1.0";

        public static Plugin Instance;
        public BepInEx.Logging.ManualLogSource log;
        public WebSocketServer wssv;

        public ConfigEntry<KeyCode> ultraHideChatKey;
        public ConfigEntry<int> socketPort;
        public bool ultraHideChat = true;

        public OnlineChatUI currentOnlineChatUI;
        public bool latestWasSmallBox = true;
        
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            Harmony harmony = new Harmony(pluginGUID);
            harmony.PatchAll();

            Instance = this;

            log = Logger;
            ultraHideChatKey = Config.Bind("Settings", "Ultra Hide Chat Key", KeyCode.Keypad3);
            socketPort = Config.Bind("Settings", "Socket Port", 8081);

            wssv = new WebSocketServer("ws://localhost:" + ((int) socketPort.BoxedValue).ToString());
            wssv.AddWebSocketService<ZeepSocketChatService>("/Chat");
            wssv.Start();
            Logger.LogInfo("WebSocket server started at ws://localhost:" + ((int)socketPort.BoxedValue).ToString());
        }

        public void Update()
        {
            if(Input.GetKeyDown((KeyCode)ultraHideChatKey.BoxedValue))
            {
                ultraHideChat = !ultraHideChat;

                if(!ultraHideChat)
                {
                    if(currentOnlineChatUI != null)
                    {
                        if(latestWasSmallBox)
                        {
                            currentOnlineChatUI.smallChatField.gameObject.SetActive(true);
                            currentOnlineChatUI.bigChatField.gameObject.SetActive(false);
                        }
                        else
                        {
                            currentOnlineChatUI.smallChatField.gameObject.SetActive(false);
                            currentOnlineChatUI.bigChatField.gameObject.SetActive(true);
                        }

                        PlayerManager.Instance.messenger.Log("ZeepSocket Chat: Chat Visible", 2f);
                    }
                }
                else
                {
                    if (currentOnlineChatUI != null)
                    {                        
                        currentOnlineChatUI.smallChatField.gameObject.SetActive(false);
                        currentOnlineChatUI.bigChatField.gameObject.SetActive(false);

                        PlayerManager.Instance.messenger.Log("ZeepSocket Chat: Chat Hidden", 2f);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            wssv.Stop();
        }
    }

    public class ZeepSocketChatService : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            Plugin.Instance.log.LogInfo("Received: " + e.Data);
            Send("Echo: " + e.Data);
        }
    }

    [HarmonyPatch(typeof(OnlineChatUI), "UpdateChatFields")]
    public static class OnlineChatUIUpdateChatFieldsPatch
    {
        public static void Postfix(OnlineChatUI __instance)
        {
            string msg = string.Join("|ZEEPSOCKETBR|", __instance.receivedChatList);

            // Check if the WebSocket server is running and if there are connected clients
            if (Plugin.Instance.wssv.IsListening)
            {
                foreach (var session in Plugin.Instance.wssv.WebSocketServices["/Chat"].Sessions.Sessions)
                {
                    session.Context.WebSocket.Send(msg);
                }
            }
        }
    }

    [HarmonyPatch(typeof(OnlineChatUI), "EnableBigBox")]
    public static class OnlineChatUIEnableBigBoxPatch
    {
        public static void Postfix(OnlineChatUI __instance)
        {
            if (Plugin.Instance.ultraHideChat)
            {
                __instance.bigChatField.gameObject.SetActive(false);
                __instance.smallChatField.gameObject.SetActive(false);
            }

            Plugin.Instance.latestWasSmallBox = false;
        }
    }

    [HarmonyPatch(typeof(OnlineChatUI), "EnableSmallBox")]
    public static class OnlineChatUIEnableSmallBoxPatch
    {
        public static void Postfix(OnlineChatUI __instance)
        {
            if (Plugin.Instance.ultraHideChat)
            {
                __instance.bigChatField.gameObject.SetActive(false);
                __instance.smallChatField.gameObject.SetActive(false);
            }

            Plugin.Instance.latestWasSmallBox = false;
        }
    }

    [HarmonyPatch(typeof(OnlineChatUI), "Awake")]
    public static class OnlineChatUIAwakePatch
    {
        public static void Postfix(OnlineChatUI __instance)
        {
            Plugin.Instance.currentOnlineChatUI = __instance;

            if (Plugin.Instance.ultraHideChat)
            {
                __instance.bigChatField.gameObject.SetActive(false);
                __instance.smallChatField.gameObject.SetActive(false);
            }

            Plugin.Instance.latestWasSmallBox = true;
        }
    }
}
