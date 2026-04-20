using UnityEngine;

namespace CharacterCustomizationTool.Editor
{
    [CreateAssetMenu(menuName = "Character Customization Tool/Slot Group Entry", fileName = "SlotGroupEntry")]
    public class SlotGroupEntry : ScriptableObject
    {
        public GroupType Type;
        public GameObject[] Variants;
    }
}