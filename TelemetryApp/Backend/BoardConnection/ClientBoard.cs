using System.IO.Ports;
using System.Diagnostics;

public class ClientBoard
{
    private SerialPort _serialPort;

    public event Action<string>? OnDataReceived;

    public void Connect(string portName)
    {
        _serialPort = new SerialPort(portName, 115200);
        _serialPort.DataReceived += SerialPort_DataReceived;
        _serialPort.Open();
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        var sp = (SerialPort)sender;
        string data = sp.ReadExisting();

        OnDataReceived?.Invoke(data);
    }

    public void Disconnect()
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
        }
    }
}