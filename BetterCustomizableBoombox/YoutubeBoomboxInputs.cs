using LethalCompanyInputUtils.Api;
using UnityEngine.InputSystem;

namespace BetterYoutubeBoombox
{
    public class YoutubeBoomboxInputs : LcInputActions
    {
        [InputAction("<Keyboard>/b", Name = "BoomboxOpenMenu", ActionId = "BoomboxOpenMenu")]
        public InputAction OpenBoomboxMenu { get; set; }
    }
}