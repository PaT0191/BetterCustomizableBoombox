using BepInEx;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BetterYoutubeBoombox.Managers
{
    public class BetterBoomboxUIManager : MonoBehaviour
    {
        public List<Animator> anim;
        public List<string> clip;

        public List<TMP_Text> texts;
        public TMP_Text urlText;

        public BoomboxMenuButtonManager playManager;
        public BoomboxMenuButtonManager pasteManager;
        public BoomboxMenuButtonManager closeManager;

        private void OnEnable()
        {
            for (int i = 0; i < anim.Count; i++)
            {
                anim[i].Play(clip[i]);
            }

            urlText.text = "";
        }

        public BetterBoomboxUIManager SetPanel(TMP_FontAsset font)
        {
            texts.ForEach(x => x.font = font);

            urlText.font = font;

            playManager.SetThisScript(0);
            pasteManager.SetThisScript(1);
            closeManager.SetThisScript(2);

            return this;
        }

        public void Play(string url)
        {
            if (url.IsNullOrWhiteSpace())
            {
                url = urlText.text;
            }

            if (!url.IsNullOrWhiteSpace())
            {
                Close();
                BoomboxController.Instance.PlaySong(url);
            }
        }

        public void Paste()
        {
            urlText.text = GUIUtility.systemCopyBuffer;
        }

        public void Close()
        {
            BoomboxController.Instance.CloseUI();
        }
    }
}

