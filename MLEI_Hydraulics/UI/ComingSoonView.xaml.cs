using System.Windows.Controls;

namespace MLEI_Hydraulics.UI
{
    public partial class ComingSoonView : UserControl
    {
        public ComingSoonView(string title, string description)
        {
            InitializeComponent();
            TitleText.Text = title;
            DescriptionText.Text = description;
        }
    }
}
