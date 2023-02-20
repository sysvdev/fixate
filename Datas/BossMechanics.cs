/*
 *      This file is part of Fixate distribution (https://github.com/vortex1409/fixate).
 *      Copyright (c) 2023 contributors
 *
 *      Fixate is free software: you can redistribute it and/or modify
 *      it under the terms of the GNU General Public License as published by
 *      the Free Software Foundation, either version 3 of the License, or
 *      (at your option) any later version.
 *
 *      Fixate is distributed in the hope that it will be useful,
 *      but WITHOUT ANY WARRANTY; without even the implied warranty of
 *      MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *      GNU General Public License for more details.
 *
 *      You should have received a copy of the GNU General Public License
 *      along with Fixate.  If not, see <https://www.gnu.org/licenses/>.
 */

namespace Fixate.Datas;

public class BossMechanics
{
    private int defaultStartTime = 0;
    private int defaultInterval = 0;
    private int defaultPlayersInvolved = 0;
    private string[] defaultMechanicNames = Array.Empty<string>();
    private int defaultWarnTime = 0;

    [JsonPropertyName("startTime")]
    [DefaultValue(0)]
    public int StartTime
    {
        get => defaultStartTime;
        set
        {
            defaultStartTime = value;
        }
    }

    [JsonPropertyName("interval")]
    [DefaultValue(0)]
    public int Interval
    {
        get => defaultInterval;
        set
        {
            defaultInterval = value;
        }
    }

    [JsonPropertyName("playersInvolved")]
    [DefaultValue(0)]
    public int PlayersInvolved
    {
        get => defaultPlayersInvolved;
        set
        {
            defaultPlayersInvolved = value;
        }
    }

    [JsonPropertyName("mechanicNames")]
    public string[] MechanicNames
    {
        get => defaultMechanicNames;
        set
        {
            defaultMechanicNames = value;
        }
    }

    [JsonPropertyName("warnTime")]
    [DefaultValue(0)]
    public int WarnTime
    {
        get => defaultWarnTime;
        set
        {
            defaultWarnTime = value;
        }
    }
}