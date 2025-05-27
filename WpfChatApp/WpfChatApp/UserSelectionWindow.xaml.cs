using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfChatApp.Model;

namespace WpfChatApp
{
    /// <summary>
    /// UserSelectionWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class UserSelectionWindow : Window
    {
        public ObservableCollection<UserInfo> Users { get; set; }
        public List<UserInfo> SelectedUsers { get; private set; }
        public ObservableCollection<UserInfo> SelectedUsersPreview { get; set; }

        //IEnumerable Collection을 읽기 전용으로 넘김
        //추후에 UserInfo currentUser를 여러명으로 확인할 수 있게 수정
        public UserSelectionWindow(IEnumerable<UserInfo> allUsers, UserInfo currentUser)
        {
            InitializeComponent();
            Users = new ObservableCollection<UserInfo>();
            SelectedUsers = new List<UserInfo>();
            SelectedUsersPreview = new ObservableCollection<UserInfo>();

            // 본인은 선택 목록에서 제외
            foreach (var user in allUsers)
            {
                if (user.Id != currentUser.Id)
                    Users.Add(user);
            }

            DataContext = this;
        }

        /// <summary>
        /// X버튼 눌렀을때 사라지게
        /// </summary>
        /// <param name="user"></param>
        private void RemoveUser(UserInfo user)
        {
            if (SelectedUsersPreview.Contains(user))
                SelectedUsersPreview.Remove(user);
        }


        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            SelectedUsers = SelectedUserListBox.Items.Cast<UserInfo>().ToList();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UserListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedUsersPreview.Clear();
            foreach (UserInfo item in UserListBox.SelectedItems)
            {
                SelectedUsersPreview.Add(item);
            }
        }

        private void UserListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (UserListBox.SelectedItem is UserInfo user && !SelectedUsersPreview.Contains(user))
            {
                SelectedUsersPreview.Add(user);
            }
        }

        private void RemoveUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UserInfo user)
            {
                SelectedUsersPreview.Remove(user);
            }
        }

        //ESC 누르면 창 닫기 Event
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}
