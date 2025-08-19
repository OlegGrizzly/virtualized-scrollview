using OlegGrizzly.VirtualizedScrollview.Core.View;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.Example
{
    public class UserCell : VirtualCell<User>
    {
        [SerializeField] private Text cellIndexText;
        [SerializeField] private Text userIdText;
        [SerializeField] private Text userNameText;
        [SerializeField] private Text userAgeText;

        private User _user;
        
        protected override void OnBound(User user, int index)
        {
            _user = user;
            
            cellIndexText.text = $"{index}";
            userIdText.text = $"{_user.Id}";
            userNameText.text = $"{_user.Name}";
            userAgeText.text = $"{_user.Age}";
        }

        protected override void OnUnbound()
        {
            _user = null;

            cellIndexText.text = "";
            userIdText.text = "";
            userNameText.text = "";
            userAgeText.text = "";
        }
    }
}