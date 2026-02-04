using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO.Ports;

public partial class ConnectionPageViewModel : ObservableObject
{
    private ClientBoard clientBoard;

    [ObservableProperty]
    private bool isPortExpanderOpen;

    [ObservableProperty]
    private string clientText;

    [ObservableProperty]
    private ObservableCollection<String> availablePorts = new();

    [ObservableProperty]
    private string? selectedPort; 


    public ConnectionPageViewModel()
    {
        clientBoard = new ClientBoard();
        clientBoard.OnDataReceived += HandleDataReceived;

        ClientText = "";

        ScanPorts();
    }

    private void HandleDataReceived(string data)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ClientText += data;
        });
    }

    [RelayCommand]
    public void Connect()
    {
        clientBoard.Connect("/dev/cu.usbserial-10");
    }


    [RelayCommand]
    public void ScanPorts()
    {
        AvailablePorts.Clear();

        string[] ports = SerialPort.GetPortNames();
        AvailablePorts = new ObservableCollection<string>(ports);

    }

    partial void OnSelectedPortChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            IsPortExpanderOpen = false; 
        }
    }

}