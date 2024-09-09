using BepInEx;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

namespace BetterYoutubeBoombox.Managers
{
    public class BoomboxMenuButtonManager : MonoBehaviour
    {
        public TMP_Text text;
        public Image image;
        public Button button;
        public EventTrigger eventTrigger;

        public Color orange;

        public void SetThisScript(int type)//type = 0 - play, 1 - paste, 2 - close
        {
            Debug.Log($"{type} - Setting");

            switch(type)
            {
                case 0:
                    button.onClick.AddListener(() => Play(""));
                    break;
                case 1:
                    button.onClick.AddListener(() => Paste());
                    break;
                case 2:
                    button.onClick.AddListener(() => Close());
                    break;
            }

            EventTrigger.Entry entry = new ();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => OnPointerEnter());
            eventTrigger.triggers.Add(entry);

            entry = new();
            entry.eventID = EventTriggerType.PointerExit;
            entry.callback.AddListener((data) => OnPointerExit());
            eventTrigger.triggers.Add(entry);
        }

        public void Play(string url)
        {
            BoomboxController.Instance.UI.Play(url);
        }

        public void Paste()
        {
            BoomboxController.Instance.UI.Paste();
        }

        public void Close()
        {
            BoomboxController.Instance.UI.Close();
        }

        public void OnPointerEnter()
        {
            text.color = Color.black;
            image.color = orange;
        }

        public void OnPointerExit()
        {
            text.color = orange;
            image.color = new Color(0f, 0f, 0f, 0.004f);
        }

        private void OnDisable()
        {
            OnPointerExit();
        }

    }
}
