using BepInEx;
using UnityEngine;

namespace BetterYoutubeBoombox
{
    internal class YoutubeBoomboxGUI : MonoBehaviour
    {
        private float menuWidth;
        private float menuHeight;
        private float menuX;
        private float menuY;

        private string url = "";

        void Awake()
        {
            menuWidth = Screen.width / 3;
            menuHeight = Screen.width / 10;
            menuX = (Screen.width / 2) - (menuWidth / 2);
            menuY = (Screen.height / 2) - ((Screen.width / 4) / 2);
        }

        public void OnGUI()
        {
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.Confined;

            GUI.Box(new Rect(menuX, menuY, menuWidth, menuHeight), "Youtube Boombox");
            url = GUI.TextField(new Rect(menuX + 25, menuY + 25, menuWidth - 125, 50), url);

            if (GUI.Button(new Rect(menuX + menuWidth - 75, menuY + 25, 50, 50), "Paste")) 
            {
                url = GUIUtility.systemCopyBuffer;
            }
            /*if (GUI.Button(new Rect(menuX + menuWidth - 75, menuY + 20, 50, 50), "Clear"))
            {
                url = "";
            }*/
            if (GUI.Button(new Rect(menuX + 25, menuY + 55 + 50, menuWidth - 50, 50), "Play"))
            {
                if (!url.IsNullOrWhiteSpace())
                {
                    if (gameObject.TryGetComponent(out BoomboxController controller))
                    {
                        controller.DestroyGUI();
                        controller.PlaySong(url);
                    }

                    UnityEngine.Cursor.visible = false;
                    //Cursor.lockState = CursorLockMode.Locked;

                    Destroy(this);
                }
            }

            if (GUI.Button(new Rect(menuX + 25, menuY + 55 + 50 + 50 + 5, menuWidth - 50, 50), "Close"))
            {

                UnityEngine.Cursor.visible = false;
                //Cursor.lockState = CursorLockMode.Locked;

                if (gameObject.TryGetComponent(out BoomboxController controller))
                {
                    controller.DestroyGUI();
                }

                Destroy(this);
            }
        }
    }
}
