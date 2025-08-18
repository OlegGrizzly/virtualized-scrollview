using UnityEngine;
using UnityEngine.UI;

namespace OlegGrizzly.VirtualizedScrollview.Controllers
{
    /// <summary>
    /// Minimal view component for one Friend row. Works with any prefab that has Texts.
    /// </summary>
    public sealed class FriendCell : MonoBehaviour
    {
        [SerializeField] private Text idText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text ageText;

        private void Reset()
        {
            // Try to auto-assign if children named accordingly
            if (!idText) idText = transform.Find("IdText")?.GetComponent<Text>();
            if (!nameText) nameText = transform.Find("NameText")?.GetComponent<Text>();
            if (!ageText) ageText = transform.Find("AgeText")?.GetComponent<Text>();
        }

        public void Set(Friend f, int index)
        {
            if (idText) idText.text = $"#{index}  {f.Id}";
            if (nameText) nameText.text = f.Name;
            if (ageText) ageText.text = f.Age.ToString();
        }
    }
}