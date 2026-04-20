using System.Linq;
using CharacterCustomizationTool.Editor.Character;

namespace CharacterCustomizationTool.Editor.SlotValidation
{
    public class FullBodyToggledRule : ISlotValidationRules
    {
        private readonly SlotType[] _slotExceptions =
        {
            SlotType.Costumes,
            SlotType.Body,
            SlotType.Faces,
        };

        public void Validate(CustomizableCharacter character, SlotType type, bool isToggled)
        {
            if (type != SlotType.Costumes || !isToggled)
            {
                return;
            }

            foreach (var slot in character.Slots.Where(s => !_slotExceptions.Contains(s.Type)))
            {
                slot.Toggle(false);
            }
        }
    }
}