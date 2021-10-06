﻿
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Motion
{
    public class MotionController
    {
        /// <summary>
        /// 網路架構編號(由 Utility 建立並寫入的網路資訊檔案編號)。
        /// </summary>
        public ushort NetworkInfoNo { get; private set; }

        /// <summary>
        /// 裝置編號(卡號)。
        /// </summary>
        public ushort DeviceNo { get; private set; }

        /// <summary>
        /// 顯示乙太網路孔連接狀態。
        /// </summary>
        public bool IsLinkUp { get; private set; }

        /// <summary>
        /// 目前已連接從站數量。
        /// </summary>
        public uint SlavesResp { get; private set; }

        /// <summary>
        /// 顯示目前整個網路 Slave EtherCAT 狀態。
        /// </summary>
        public AlStates AlState { get; private set; }

        /// <summary>
        /// 顯示 EtherCAT 工作計數器數值。
        /// </summary>
        public uint WorkingCounter { get; private set; }

        /// <summary>
        /// 指定結果操作的嘗試次數，預設為5次。
        /// </summary>
        public static uint RetryCount { get; private set; } = 5;

        /// <summary>
        /// 指定重試嘗試之間的間隔(ms)。
        /// </summary>
        public static int RetryInterval { get; private set; } = 200;

        private MotionSlave[] slavesItems;

        /// <summary>
        /// 所有從站模組。
        /// </summary>
        public MotionSlave[] SlaveItems
        {
            get => slavesItems;
            private set => slavesItems = value.Length > EtherCatDef.NETWORK_SLAVE_MAX ?
                slavesItems = new MotionSlave[EtherCatDef.NETWORK_SLAVE_MAX] : slavesItems = value;
        }

        #region Event

        public event EventHandler<DeviceStateChangeEventArgs> DeviceStateChangeEvent;

        public event EventHandler<SalveAxisStateChangeEventArgs> SalveAxisStateChangeEvent;

        #endregion

        private readonly byte EnumCycleTime = EtherCatDef.DEV_OP_CYCLE_TIME_1MS;

        public MotionController(ushort deviceNo, ushort networkInfoNo = 0)
        {
            DeviceNo = deviceNo;
            NetworkInfoNo = networkInfoNo;
        }

        #region private Method

        private void OnDeviceStateChangeEvent()
        {
            DeviceStateChangeEvent?.Invoke(this, new DeviceStateChangeEventArgs()
            {
                DeviceNo = DeviceNo,
                IsLinkUp = IsLinkUp,
                SlavesResp = SlavesResp,
                AlState = AlState,
                WorkingCounter = WorkingCounter
            });
        }

        #endregion

        #region Static Method

        /// <summary>
        /// 取得當前使用的 dll 版本資訊。
        /// </summary>
        /// <param name="resultCode"></param>
        /// <returns></returns>
        public static ushort GetVersion(ref int resultCode)
        {
            var str = new StringBuilder();
            ushort size = 0;
            int i = 0;
            do
            {
                // ECAT-M801 實體卡片上可以更改卡號
                // 傳入 Card ID(卡號)，取得目前卡片上可使用裝置的數量
                resultCode = EtherCatLib.ECAT_GetDllVersion(str, ref size);
                if (resultCode != 0)
                {
                    SpinWait.SpinUntil(() => false, 200);
                }
            } while (resultCode != 0 && i++ < 10);
            if (resultCode == 0)
            {
                return size;
            }
            else
            {
                Logger.Error(resultCode, MethodBase.GetCurrentMethod().Name);
                return 0;
            }
        }

        /// <summary>
        /// 取得可用裝置數量。
        /// </summary>
        /// <param name="cardId">軸卡ID。</param>
        /// <returns></returns>
        public static ushort GetDeviceCount(byte[] cardId, ref int resultCode)
        {
            ushort deviceCnt = 0;
            int i = 0;
            do
            {
                // ECAT-M801 實體卡片上可以更改卡號
                // 傳入 Card ID(卡號)，取得目前卡片上可使用裝置的數量
                resultCode = EtherCatLib.ECAT_GetDeviceCnt(ref deviceCnt, cardId);
                if (resultCode != 0)
                {
                    SpinWait.SpinUntil(() => false, 200);
                }
            } while (resultCode != 0 && i++ < 10);
            if (resultCode == 0)
            {
                return deviceCnt;
            }
            else
            {
                Logger.Error(resultCode, MethodBase.GetCurrentMethod().Name);
                return 0;
            }
        }

        #endregion

        /// <summary>
        /// 開啟裝置做通訊操作。
        /// </summary>
        /// <param name="resultCode"></param>
        /// <returns></returns>
        public bool OpenDevice(ref int resultCode)
        {
            int i = 0;
            do
            {
                // 開啟指定裝置編號(卡號)來做通訊操作。
                resultCode = EtherCatLib.ECAT_OpenDevice(DeviceNo);
                if (resultCode != 0)
                {
                    SpinWait.SpinUntil(() => false, RetryInterval);
                }
            } while (resultCode != 0 && i++ < RetryCount);
            if (resultCode == 0)
            {
                return true;
            }
            else
            {
                Logger.Error(resultCode, MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        /// <summary>
        /// 取得 EtherCAT 網絡狀態。
        /// </summary>
        /// <returns></returns>
        public bool GetDeviceState(ref int resultCode)
        {
            uint linkup = 0, slaveResp = 0, alstates = 0, wc = 0;
            int i = 0;
            do
            {
                resultCode = EtherCatLib.ECAT_GetDeviceState(DeviceNo, ref linkup, ref slaveResp, ref alstates, ref wc);
                if (resultCode != 0)
                {
                    SpinWait.SpinUntil(() => false, RetryInterval);
                }
            } while (resultCode != 0 && i++ < RetryCount);
            if (resultCode == 0)
            {
                IsLinkUp = linkup == 1;
                SlavesResp = slaveResp;
                AlState = (AlStates)alstates;
                WorkingCounter = wc;
                OnDeviceStateChangeEvent();
                return true;
            }
            else
            {
                Logger.Error(resultCode, MethodBase.GetCurrentMethod().Name);
                return false;
            }
        }

        /// <summary>
        /// 開始 EtherCAT 操作任務。
        /// </summary>
        /// <param name="resultCode"></param>
        /// <returns></returns>
        public bool StartOpTask(ref int resultCode)
        {
            int i = 0;
            while (GetDeviceState(ref resultCode) && i++ < RetryCount)
            {
                if (AlState == AlStates.ECAT_AS_OP)
                {
                    return true;
                }
                else
                {
                    resultCode = AlState == AlStates.ECAT_AS_PREOP
                        ? EtherCatLib.ECAT_StartDeviceOpTask(DeviceNo, NetworkInfoNo, EnumCycleTime, RetryCount)
                        : EtherCatLib.ECAT_StopDeviceOpTask(DeviceNo);
                }
                SpinWait.SpinUntil(() => false, RetryInterval);
            }
            if (resultCode != 0)
            {
                Logger.Error(resultCode, MethodBase.GetCurrentMethod().Name);
            }
            return false;
        }

        /// <summary>
        /// 裝置、從站初始化。
        /// </summary>
        /// <param name="resultCode"></param>
        /// <returns></returns>
        public bool InitializeSlave(ref int resultCode)
        {
            if (OpenDevice(ref resultCode) && StartOpTask(ref resultCode))
            {
                if (AlState != AlStates.ECAT_AS_OP)
                {
                    Logger.Error(resultCode, MethodBase.GetCurrentMethod().Name, "裝置不在Operational狀態，無法取得從站資訊。");
                    return false;
                }
                SlaveItems = new MotionSlave[SlavesResp];
                for (int i = 0; i < SlaveItems.Length; i++)
                {
                    SlaveItems[i].GetSlaveInfo(ref resultCode);
                }
            }
            return true;
        }

        /// <summary>
        /// 從站之子軸初始化。
        /// </summary>
        /// <returns></returns>
        public bool InitMotionAxis()
        {
            for (int i = 0; i < SlaveItems.Length; i++)
            {
                var mcSlaveNo = new List<ushort>();
                var mcSubAxisNo = new List<ushort>();
                MotionSlave slave = SlaveItems[i];
                if (slave.AxisItems == null)
                {
                    continue;
                }
                for (int j = 0; j < slave.AxisItems.Length; j++)
                {
                    // 軸
                    MotionAxis axis = slave.AxisItems[j];
                    int resultCode = 0; // 回傳值
                    int retryCount = 0; // 重試次數
                    do
                    {
                        if (axis.GetAxisState(ref resultCode))
                        {
                            if (resultCode == EtherCatError.ECAT_ERR_MC_NOT_INITIALIZED) // 軸尚未初始化。
                            {
                                axis.IsMcInitOk = false;
                                mcSlaveNo.Add(slave.SlaveNo);
                                mcSubAxisNo.Add(axis.AxisNo);
                                break;
                            }
                            else if (axis.AxisState == AxisStates.MC_AS_DISABLED) // 軸已初始化，Servo尚未啟動。
                            {
                                axis.IsMcInitOk = true;
                                break;
                            }
                            else if (axis.AxisState == AxisStates.MC_AS_STANDSTILL) // 軸已初始化，Servo啟動，停止中。
                            {
                                axis.IsMcInitOk = true;
                                // Servo尚未停止，執行 ServoOff。
                                axis.ServoControl(false, ref resultCode);
                            }
                            else if (axis.AxisState == AxisStates.MC_AS_ERRORSTOP) // 軸出現異常。
                            {
                                axis.IsMcInitOk = true;
                                axis.GetAxisErrorState(ref resultCode);
                                SalveAxisStateChangeEvent?.Invoke(slave, new SalveAxisStateChangeEventArgs()
                                {
                                    SlaveNo = slave.SlaveNo,
                                    AlState = slave.AlState,
                                    SlaveName = slave.SlaveName,
                                    AxisNo = axis.AxisNo,
                                    AxisState = axis.AxisState
                                });
                                Logger.Error(resultCode, MethodBase.GetCurrentMethod().Name, $"SlaveNo=[{slave.SlaveNo}], AxisNo=[{axis.AxisNo}] MC_AS_ERRORSTOP !!!");
                                break;
                            }
                            else if (axis.AxisState != AxisStates.MC_AS_STOPPING) // 軸仍在運動中。
                            {
                                axis.IsMcInitOk = true;
                                // 如果不在停止狀態，立刻停止。
                                axis.AxisQuickStop(ref resultCode);
                            }
                        }
                        SpinWait.SpinUntil(() => false, RetryInterval);
                    }
                    while (retryCount++ < RetryCount);
                }
                if (mcSlaveNo.Count > 0 && mcSubAxisNo.Count > 0)
                {
                    int k = 0;
                    int ret;
                    do
                    {
                        ret = EtherCatLib.ECAT_McInit(
                            slave.DeviceNo,
                            mcSlaveNo.ToArray(),
                            mcSubAxisNo.ToArray(),
                            (ushort)mcSubAxisNo.Count);
                        if (ret != 0)
                        {
                            SpinWait.SpinUntil(() => false, RetryInterval);
                        }
                    } while (ret != 0 && k++ < RetryCount);
                    if (ret == 0)
                    {
                        slave.IsMcInitOk = true;
                        foreach (MotionAxis axis in slave.AxisItems)
                        {
                            axis.IsMcInitOk = true;
                        }
                    }
                    else
                    {
                        slave.IsMcInitOk = false;
                        foreach (MotionAxis axis in slave.AxisItems)
                        {
                            axis.IsMcInitOk = false;
                        }
                        Logger.Error(ret, "ECAT_McInit", $"SlaveNo=[{slave.SlaveNo}] Error !!!");
                    }
                }
            }
            return true;
        }
    }
}
