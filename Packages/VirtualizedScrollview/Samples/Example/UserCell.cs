using System.Threading.Tasks;
using OlegGrizzly.VirtualizedScrollview.Core.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.Example
{
    public class UserCell : VirtualCell<User>
    {
        [SerializeField] private TMP_Text cellIndexText;
        [SerializeField] private TMP_Text userIdText;
        [SerializeField] private TMP_Text userNameText;
        [SerializeField] private TMP_Text userAgeText;

        private User _user;
        
        protected override Task OnBound(User user, int index)
        {
            _user = user;
            
            cellIndexText.text = $"{index}";
            userIdText.text = $"{_user.Id}";
            userNameText.text = $"{_user.Name}";
            userAgeText.text = $"{_user.Age}";
            
            return Task.CompletedTask;
        }

        protected override void OnUnbound()
        {
            _user = null;

            cellIndexText.text = "";
            userIdText.text = "";
            userNameText.text = "";
            userAgeText.text = "";
        }
        
        public float Measure(User u, float totalWidth, float baseItemHeight)
        {
            var width = Mathf.Max(0f, totalWidth);
            
            var hIndex = cellIndexText ? MeasureTMPTextHeight(cellIndexText, u != null ? "0" : string.Empty, width) : 0f;
            var hId = userIdText ? MeasureTMPTextHeight(userIdText, u != null ? u.Id.ToString() : string.Empty, width) : 0f;
            var hName = userNameText ? MeasureTMPTextHeight(userNameText, u != null ? u.Name ?? string.Empty : string.Empty, width) : 0f;
            var hAge = userAgeText ? MeasureTMPTextHeight(userAgeText, u != null ? u.Age.ToString() : string.Empty, width) : 0f;
            
            var contentHeight = hIndex + hId + hName + hAge;

            return Mathf.Max(baseItemHeight, Mathf.Ceil(contentHeight));
        }
        
        private static float MeasureTextHeight(Text text, string value, float width)
        {
            var settings = text.GetGenerationSettings(new Vector2(width, Mathf.Infinity));
            var preferred = text.cachedTextGeneratorForLayout.GetPreferredHeight(value, settings);
            
            return preferred / text.pixelsPerUnit;
        }

        private static float MeasureTMPTextHeight(TMP_Text text, string value, float width)
        {
            if (text == null) return 0f;
            
            var preferredValues = text.GetPreferredValues(value, width, Mathf.Infinity);
            
            return preferredValues.y;
        }
    }
}