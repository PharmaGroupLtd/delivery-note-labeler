using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DeliveryNoteLabeler.Services;

public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string DocName = "Delivery Note Labeler";

        [MarshalAs(UnmanagedType.LPWStr)]
        public string OutputFile = string.Empty;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string DataType = "RAW";
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out nint printerHandle, nint defaults);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(nint printerHandle);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartDocPrinter(nint printerHandle, int level, [In] DocInfo documentInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(nint printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(nint printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(nint printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(
        nint printerHandle,
        byte[] bytes,
        int count,
        out int bytesWritten);

    public static void SendRaw(string printerName, ReadOnlySpan<byte> data)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new InvalidOperationException("No printer is configured. Open Settings and select a printer.");
        }

        if (!OpenPrinter(printerName, out var printerHandle, nint.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot open printer \"{printerName}\".");
        }

        try
        {
            var documentInfo = new DocInfo();
            if (!StartDocPrinter(printerHandle, 1, documentInfo))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot start print job on \"{printerName}\".");
            }

            try
            {
                if (!StartPagePrinter(printerHandle))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot start print page on \"{printerName}\".");
                }

                try
                {
                    var buffer = data.ToArray();
                    if (!WritePrinter(printerHandle, buffer, buffer.Length, out var written) || written != buffer.Length)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to send label data to \"{printerName}\".");
                    }
                }
                finally
                {
                    EndPagePrinter(printerHandle);
                }
            }
            finally
            {
                EndDocPrinter(printerHandle);
            }
        }
        finally
        {
            ClosePrinter(printerHandle);
        }
    }

    public static void SendRaw(string printerName, string zpl)
    {
        SendRaw(printerName, Encoding.UTF8.GetBytes(zpl));
    }
}
