/*
 * Copyright (c) Contributors http://github.com/aduffy70/Visit-Logger
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Visit-Logger Module nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Reflection;
using System.Timers;

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace VisitLoggerModule
{
    public class VisitLoggerModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        IDialogModule m_dialogmod;
        int m_blockTime;
        Dictionary<string, DateTime> m_recentVisits = new Dictionary<string, DateTime>();
        string m_logPath;
        bool m_localLog;
        string m_googleAccount;
        private Scene m_scene;
        bool m_enabled;

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            IConfig visitLoggerConfig = config.Configs["VisitLogger"];
            if (visitLoggerConfig != null)
            {
                m_enabled = visitLoggerConfig.GetBoolean("enabled", false);
                m_blockTime = visitLoggerConfig.GetInt("block_time", 3600);
                m_localLog = visitLoggerConfig.GetBoolean("local_log", true);
                m_logPath = visitLoggerConfig.GetString("log_path", "");
                m_googleAccount = visitLoggerConfig.GetString("google_account", "NO_ACCOUNT");
            }
            if (m_enabled)
            {
                m_scene = scene;
                m_log.Info("[VisitLogger] Initialized...");
                m_log.Info(String.Format("[VisitLogger] Block Time: {0}, Local Log: {1}, Log Path: {2}, Google Account: {3}", m_blockTime, m_localLog, m_logPath, m_googleAccount));
            }
        }

        public void PostInitialise()
        {
            if (m_enabled)
            {
                m_scene.EventManager.OnMakeRootAgent += new EventManager.OnMakeRootAgentDelegate(OnVisit);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get
            {
                return "VisitLoggerModule";
            }
        }

        public bool IsSharedModule
        {
            get
            {
                return false;
            }
        }

        #endregion

        void OnVisit(ScenePresence presence)
        {
            string visitorName = presence.Firstname + "_" + presence.Lastname;
            DateTime now = DateTime.Now;
            if (m_recentVisits.ContainsKey(visitorName))
            {
                if (now.Subtract(m_recentVisits[visitorName]).TotalSeconds > m_blockTime)
                {
                    LogVisit(presence, now);
                    m_recentVisits[visitorName] = now;
                }
                else
                {
                    m_recentVisits[visitorName] = now;
                }
            }
            else
            {
                LogVisit(presence, now);
                m_recentVisits.Add(visitorName, now);
            }
        }

        void LogVisit(ScenePresence presence, DateTime now)
        {
            if (m_localLog)
            {
                string logString = String.Format("{0},{1} {2},{3}", m_scene.RegionInfo.RegionName, presence.Firstname, presence.Lastname, now);
                //m_log.Info("[VisitLogger] " + logString); //DEBUG
                string logFile = System.IO.Path.Combine(m_logPath, "VisitLog.csv");
                if (!System.IO.File.Exists(logFile))
                {
                    //Add a header row
                    logString = "Region,Name,Date-Time\n" + logString;
                }
                System.IO.StreamWriter dataLog = System.IO.File.AppendText(logFile);
                dataLog.WriteLine(logString);
                dataLog.Close();
            }
            else
            {
                string logString = String.Format("logvisit?account={0}&region={1}&name={2} {3}&datetime={4}", m_googleAccount, m_scene.RegionInfo.RegionName, presence.Firstname, presence.Lastname, now);
                WebRequest logVisitRequest = WebRequest.Create(System.IO.Path.Combine(m_logPath, logString));
                StreamReader urlData = new StreamReader(logVisitRequest.GetResponse().GetResponseStream());
                //m_log.Info("[VisitLogger] " + System.IO.Path.Combine(m_logPath, logString)); //DEBUG
            }
        }
    }
}
