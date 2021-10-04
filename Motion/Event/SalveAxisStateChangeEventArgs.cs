﻿namespace Motion
{
    public class SalveAxisStateChangeEventArgs
    {
        public ushort SlaveNo { get; set; }

        public AlStates AlState { get; set; }

        public string SlaveName { get; set; }

        public ushort SubAxisNo { get; set; }

        public AxisStates AxisState { get; set; }
    }
}