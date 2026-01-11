using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class SessionDetailViewModel : ObservableObject
{

    public ObservableCollection<SinglePlotViewModel> VisibleCharts { get; set; }
    public SessionDetailViewModel()
    {
    }

    


}