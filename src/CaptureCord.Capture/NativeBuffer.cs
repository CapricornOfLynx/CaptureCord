// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;

namespace CaptureCord.Capture;

internal static unsafe class NativeBuffer
{
    public static byte[] CopyRgb32(Com buffer, int width, int height, int defaultStride)
    {
        var rowBytes = checked(width * 4);
        var pixels = new byte[checked(rowBytes * height)];
        var iid = new Guid("7dc9d5f9-9ed9-44ec-9bbf-0600bb589fbb");
        nint twoD = 0;
        var hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)buffer.Slot(0))(buffer.Pointer, &iid, &twoD);
        if (hr >= 0)
        {
            using var surface = new Com(twoD);
            nint firstRow;
            int pitch;
            Native.Check(((delegate* unmanaged[Stdcall]<nint, nint*, int*, int>)surface.Slot(3))(twoD, &firstRow, &pitch));
            try
            {
                if (Math.Abs((long)pitch) < rowBytes) throw new IOException("Invalid video surface pitch.");
                // Lock2D returns logical scanline zero, including for negative pitch.
                for (int y = 0; y < height; y++) Marshal.Copy(firstRow + checked(y * pitch), pixels, y * rowBytes, rowBytes);
            }
            finally { Native.Check(((delegate* unmanaged[Stdcall]<nint, int>)surface.Slot(4))(twoD)); }
            return pixels;
        }
        if (hr != unchecked((int)0x80004002)) Native.Check(hr);
        nint data;
        uint maximum, length;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, nint*, uint*, uint*, int>)buffer.Slot(3))(buffer.Pointer, &data, &maximum, &length));
        try
        {
            var pitch = checked((int)Math.Abs((long)defaultStride));
            if (pitch < rowBytes || (long)pitch * (height - 1) + rowBytes > length) throw new IOException("The device returned an invalid video buffer.");
            for (int y = 0; y < height; y++)
            {
                var row = defaultStride < 0 ? height - 1 - y : y;
                Marshal.Copy(data + checked(row * pitch), pixels, y * rowBytes, rowBytes);
            }
            return pixels;
        }
        finally { Native.Check(((delegate* unmanaged[Stdcall]<nint, int>)buffer.Slot(4))(buffer.Pointer)); }
    }
}
