﻿using System;
using System.Collections.Generic;
using System.Text;

namespace MowControl
{
    public enum LogType
    {
        MowControllerStarted = 1,
        PowerOn = 2,
        PowerOff = 3,
        Failure = 4,
        MowerCame = 5,
        MowerLeft = 6,
        MowerLost = 7,
        MowerStuckInHome = 8,
        MowingStarted = 9,
        MowingEnded = 10,
        DailyReport = 11,
        HourlyReport = 12,
        Debug = 13,
        NewDay = 14,
    }
}
