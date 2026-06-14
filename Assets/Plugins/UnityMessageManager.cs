using UnityEngine;
using System.Runtime.InteropServices;

namespace Azesmway.Unity
{
    public class UnityMessageManager : MonoBehaviour
    {
        public static UnityMessageManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SendMessageToRN(string message)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (AndroidJavaClass jc = new AndroidJavaClass("com.azesmwayreactnativeunity.ReactNativeUnityViewManager"))
                {
                    jc.CallStatic("sendMessageToMobileApp", message);
                }
#elif UNITY_IOS && !UNITY_EDITOR
                onUnityMessage(message);
#else
            Debug.Log($"[UnityToRN Editor Mode]: {message}");
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void onUnityMessage(string message);
#endif
    }
}