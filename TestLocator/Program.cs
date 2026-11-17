using System;
using System.Management;

class Program
{
    static void Main()
    {
        try
        {
            var watcher = new ManagementEventWatcher(new ManagementScope(@"root\wmi"), new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
            watcher.Start();
            Console.WriteLine("WmiMonitorBrightnessEvent watcher started successfully!");
            watcher.Stop();
        }
        catch (Exception ex)
        {
            Console.WriteLine("WmiMonitorBrightnessEvent NOT supported: " + ex.Message);
        }
    }
}
