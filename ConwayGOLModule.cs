/*
 * Copyright (c) Contributors http://github.com/aduffy70/ConwayGOL
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the ConwayGOL Module nor the
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

using Mono.Addins;

[assembly: Addin("ConwaysGOLModule", "0.1")]
[assembly: AddinDependency("OpenSim", "0.5")]
namespace ConwayGOLModule
{
    [Extension(Path="/OpenSim/RegionModules",NodeName="RegionModule")]
    public class ConwayGOLModule : INonSharedRegionModule
    {
        //Set up logging and dialog messages
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        IDialogModule m_dialogmod;
        //Configurable settings
        bool m_enabled = false;
        int m_channel;  //Channel for chat commands
        int m_cycleTime; //Time in milliseconds between cycles
        float m_xCenter; //inworld x coordinate for the 0,0,0 position
        float m_yCenter;  //inworld y coordinate for the 0,0,0 position
        float m_zCenter; //inworld z coordinate for the 0,0,0 position
        float m_aRadius; //overall torus radius
        float m_bRadius; //torus tube radius
        int m_xCells;
        int m_yCells;

        List<SceneObjectGroup> m_prims = new List<SceneObjectGroup>(); //list of objects managed by this module
        int[] m_cellStatus; // live(1)-dead(0) status for each cell in the matrix
        Color4 m_deadColor = new Color4(0f, 0f, 0f, 0.25f); //color for dead cells
        Color4 m_liveColor = new Color4(1.0f, 1f, 1f, 1.0f); //color for live cells
        bool m_running = false; //Keep track of whether the game is running
        Timer m_timer = new Timer(); //Timer to replace the region heartbeat
        Scene m_scene;
        List<int> m_activeCells = new List<int>(); //Indices of cells which could possibly change on the next cycle
        string m_patternUrlPath;


        #region INonSharedRegionModule interface

        public void Initialise(IConfigSource config)
        {
            IConfig conwayConfig = config.Configs["ConwayGOL"];
            if (conwayConfig != null)
            {
                m_enabled = conwayConfig.GetBoolean("enabled", false);
                m_cycleTime = conwayConfig.GetInt("cycle_time", 5) * 1000;
                m_channel = conwayConfig.GetInt("chat_channel", 9);
                m_xCells = conwayConfig.GetInt("x_cells", 36);
                m_yCells = conwayConfig.GetInt("y_cells", 36);
                m_xCenter = conwayConfig.GetFloat("x_position", 128);
                m_yCenter = conwayConfig.GetFloat("y_position", 128);
                m_zCenter = conwayConfig.GetFloat("z_position", 40);
                m_aRadius = conwayConfig.GetFloat("a_radius", 20);
                m_bRadius = conwayConfig.GetFloat("b_radius", 15);
                m_patternUrlPath = conwayConfig.GetString("pattern_url_path");
            }
            if (m_enabled)
            {
                m_log.Info("[ConwayGOL] Initializing...");
            }
        }

        public void AddRegion(Scene scene)
        {
            if (m_enabled)
            {
                m_scene = scene;
                m_dialogmod = scene.RequestModuleInterface<IDialogModule>();
                m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
                m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
                m_timer.Elapsed += new ElapsedEventHandler(OnTimer);
                m_timer.Interval = m_cycleTime;
                m_cellStatus = new int[m_xCells * m_yCells];
                SetupMatrix(m_scene);
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get
            {
                return "ConwayGOLModule";
            }
        }

        public Type ReplaceableInterface
        {
            get
            {
                return null;
            }
        }

        #endregion

        void SetupMatrix(Scene scene)
        {
            // We're going to place a torus of dead cells in world
            float twoPi = 2f * (float)Math.PI;
            float uSpacing = twoPi / m_xCells;
            float vSpacing = twoPi / m_yCells;
            float uRadians = 0;
            float vRadians = 0;
            int counter = 0;
            for (int y=0; y<m_yCells; y++)
            {
                for (int x=0; x<m_xCells; x++)
                {
                    //Calculate the cell's position
                    float xPos = m_xCenter + ((m_aRadius + (m_bRadius * (float)Math.Cos(vRadians))) * (float)Math.Cos(uRadians));
                    float yPos = m_yCenter + ((m_aRadius + (m_bRadius * (float)Math.Cos(vRadians))) * (float)Math.Sin(uRadians));
                    float zPos = m_zCenter + (m_bRadius * (float)Math.Sin(vRadians));
                    Vector3 pos = new Vector3(xPos, yPos, zPos);
                    //Set the size, shape, texture, and color of the cell
                    PrimitiveBaseShape prim = PrimitiveBaseShape.CreateSphere();
                    prim.Textures = new Primitive.TextureEntry(new UUID("5748decc-f629-461c-9a36-a35a236fe36f")); //blank texture
                    SceneObjectGroup sog = new SceneObjectGroup(UUID.Zero, pos, prim);
                    float size = 0.75f + (Math.Abs(((m_xCells-1) / 2f) - (float)x) / ((m_xCells-1) / 3f));
                    sog.RootPart.Scale = new Vector3(size, size, size);
                    Primitive.TextureEntry tex = sog.RootPart.Shape.Textures;
                    m_cellStatus[counter] = 0;
                    tex.DefaultTexture.RGBA = m_deadColor;
                    sog.RootPart.UpdateTexture(tex);
                    sog.RootPart.UpdatePrimFlags(false, false, true, false);
                    //Add the cell to the list of managed objects
                    m_prims.Add(sog);
                    vRadians = vRadians + vSpacing;
                    counter++;
                }
                uRadians = uRadians + uSpacing;
            }
            //Place the managed objects visibly into the scene
            m_running = false;
            foreach (SceneObjectGroup sogr in m_prims)
            {
                scene.AddNewSceneObject(sogr, false);
            }
        }

        void OnChat(Object sender, OSChatMessage chat)
        {
            if (chat.Channel != m_channel)
            {
                //Message is not for this module
                return;
            }
            else if (chat.Message == "reset")
            {
                if (m_running)
                {
                    StopGameOfLife();
                }
                if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("ConwayGOL Module: Reset...");
                }
                ResetAllCells();
            }
            else if (chat.Message == "start")
            {
                if (!m_running)
                {
                    if (m_dialogmod != null)
                    {
                        m_dialogmod.SendGeneralAlert("ConwayGOL Module: Start...");
                    }
                    StartGameOfLife();
                }
                else
                {
                    if (m_dialogmod != null)
                    {
                        m_dialogmod.SendGeneralAlert("ConwayGOL Module: Already running...");
                    }
                    m_log.Info("[ConwayGOL] Already running...");
                }
            }
            else if (chat.Message == "stop")
            {
                if (m_running)
                {
                    if (m_dialogmod != null)
                    {
                        m_dialogmod.SendGeneralAlert("ConwayGOL Module: Stop...");
                    }
                    StopGameOfLife();
                }
                else
                {
                    if (m_dialogmod != null)
                    {
                        m_dialogmod.SendGeneralAlert("ConwayGOL Module: Already stopped...");
                    }
                    m_log.Info("[ConwayGOL] Not running...");
                }
            }
            else if (chat.Message == "example")
            {
                if (m_running)
                {
                    StopGameOfLife();
                }
                ResetAllCells();
                LoadExamplePattern();
                if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("ConwayGOL Module: Loaded 'example'...");
                }
            }
            else
            {
                //Read starting pattern from a url
                string fileName = System.IO.Path.Combine(m_patternUrlPath, chat.Message);
                if (m_running)
                {
                    StopGameOfLife();
                }
                ResetAllCells();
                LoadFromUrl(fileName);
            }
        }

        void OnTimer(object source, ElapsedEventArgs e)
        {
            if (m_activeCells.Count > 0)
            {
                //Make copies of the status list and active cells list so we can update the originals while using or iterating through the copies
                int[] oldCellStatus = new int[m_xCells * m_yCells];
                Array.Copy(m_cellStatus, oldCellStatus, m_xCells * m_yCells);
                List<int> oldActiveCells = new List<int>(m_activeCells.ToArray());
                m_activeCells.Clear();
                foreach(int cellIndex in oldActiveCells)
                {
                    List<int> neighborIndices = GetNeighbors(cellIndex);
                    int liveNeighbors = 0;
                    foreach(int neighbor in neighborIndices)
                    {
                        liveNeighbors = liveNeighbors + oldCellStatus[neighbor];
                    }
                    if (oldCellStatus[cellIndex] == 0)
                    {
                        if (liveNeighbors == 3)
                        {
                            SetLive(cellIndex);
                        }
                    }
                    else if (oldCellStatus[cellIndex] == 1)
                    {
                        if ((liveNeighbors == 3) || (liveNeighbors == 2))
                        {
                            SetLive(cellIndex);
                        }
                        else
                        {
                            SetDead(cellIndex);
                        }
                    }
                    else
                    {
                        m_log.Info("[ConwayGOL] Invalid value!");
                    }
                }
            }
            else
            {
                StopGameOfLife();
            }
        }

        void LoadFromUrl(string fileName)
        {
            try
            {
                WebRequest patternUrl = WebRequest.Create(fileName);
                StreamReader urlData = new StreamReader(patternUrl.GetResponse().GetResponseStream());
                string line;
                while ((line = urlData.ReadLine()) != null)
                {
                    string[] cellCoordinates = line.Split(',');
                    SetLive(Int32.Parse(cellCoordinates[0]), Int32.Parse(cellCoordinates[1]));
                }
                if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("ConwayGOL Module: Loaded \'" + fileName + "\'...");
                }
            }
            catch
            {
                m_log.Info("[ConwayGOL] Error loading parameters from file \"" + fileName + "\"...");
                if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("ConwayGOL Module: Unable to load \'" + fileName + "\'...");
                }
            }
        }

        int Index(int xPos, int yPos)
        {
            //Convert x,y matrix indices into a index count
            return yPos * m_xCells + xPos;
        }

        void SetLive(int xPos, int yPos)
        {
            SetLive(Index(xPos, yPos));
        }

        void SetLive(int index)
        {
            //Change the appearance and update the status list
            Primitive.TextureEntry tex = m_prims[index].RootPart.Shape.Textures;
            m_cellStatus[index] = 1;
            tex.DefaultTexture.RGBA = m_liveColor;
            m_prims[index].RootPart.UpdateTexture(tex);
            //Add the live cell to the active cell list (if it isn't already there)
            if (!m_activeCells.Contains(index))
            {
                m_activeCells.Add(index);
            }
            //Add each neighbor cell to the active cell list (if they aren't already there)
            List<int> neighborIndices = GetNeighbors(index);
            foreach(int neighbor in neighborIndices)
            {
                if (!m_activeCells.Contains(neighbor))
                {
                    m_activeCells.Add(neighbor);
                }
            }
        }

        void SetDead(int xPos, int yPos)
        {
            SetDead(Index(xPos, yPos));
        }

        void SetDead(int index)
        {
            Primitive.TextureEntry tex = m_prims[index].RootPart.Shape.Textures;
            m_cellStatus[index] = 0;
            tex.DefaultTexture.RGBA = m_deadColor;
            m_prims[index].RootPart.UpdateTexture(tex);
        }

        List<int> GetNeighbors(int index)
        {
            int x = index % m_xCells;
            int y = index / m_xCells;
            return GetNeighbors(x, y);
        }

        List<int> GetNeighbors(int xPos, int yPos)
        {
            //Get a list of the indices of the neighbors of a given cell
            int rowAbove = new int();
            int rowBelow = new int();
            int colLeft = new int();
            int colRight = new int();
            if (yPos == 0)
            {
                rowAbove = yPos + 1;
                rowBelow = m_yCells - 1;
            }
            else if (yPos == m_yCells - 1)
            {
                rowAbove = 0;
                rowBelow = yPos - 1;
            }
            else
            {
                rowAbove = yPos + 1;
                rowBelow = yPos - 1;
            }
            if (xPos == 0)
            {
                colRight = xPos + 1;
                colLeft = m_xCells - 1;
            }
            else if (xPos == m_xCells - 1)
            {
                colRight = 0;
                colLeft = xPos - 1;
            }
            else
            {
                colRight = xPos + 1;
                colLeft = xPos - 1;
            }
            List<int> neighbors = new List<int>();
            neighbors.Add(Index(colLeft, rowBelow));
            neighbors.Add(Index(colLeft, yPos));
            neighbors.Add(Index(colLeft, rowAbove));
            neighbors.Add(Index(xPos, rowBelow));
            neighbors.Add(Index(xPos, rowAbove));
            neighbors.Add(Index(colRight, rowBelow));
            neighbors.Add(Index(colRight, yPos));
            neighbors.Add(Index(colRight, rowAbove));
            return neighbors;
        }

        void ResetAllCells()
        {
            //Stop timer, set all cells dead, and clear the list of active cells
            m_log.Info("[ConwayGOL] resetting...");
            for (int countprims = 0; countprims < (m_xCells * m_yCells); countprims ++)
            {
                if (m_cellStatus[countprims] == 1)
                {
                    SetDead(countprims);
                }
            }
            m_activeCells.Clear();
        }

        void StartGameOfLife()
        {
            //Start timer
            if (m_activeCells.Count == 0)
            {
                m_log.Info("[ConwayGOL] No live cells.  Not starting...");
            }
            else
            {
                m_log.Info("[ConwayGOL] Starting...");
                m_running = true;
                m_timer.Start();
            }
        }

        void StopGameOfLife()
        {
            m_log.Info("[ConwayGOL] Stopping...");
            m_running = false;
            m_timer.Stop();
        }

        void LoadExamplePattern()
        {
            //load an example pattern
            //Glider
            SetLive(17, 26);
            SetLive(18, 26);
            SetLive(19, 26);
            SetLive(19, 27);
            SetLive(18, 28);
            //Glider
            SetLive(26, 17);
            SetLive(27, 17);
            SetLive(26, 18);
            SetLive(28, 18);
            SetLive(26, 19);
            //Glider
            SetLive(18, 8);
            SetLive(17, 9);
            SetLive(17, 10);
            SetLive(18, 10);
            SetLive(19, 10);
            //Glider
            SetLive(10, 17);
            SetLive(8, 18);
            SetLive(10, 18);
            SetLive(9, 19);
            SetLive(10, 19);
            //Cross
            SetLive(0, 0);
            SetLive(1, 0);
            SetLive(35, 0);
            //Cross
            SetLive(18, 17);
            SetLive(18, 18);
            SetLive(18, 19);
            //Cross
            SetLive(0, 17);
            SetLive(0, 18);
            SetLive(0, 19);
            //Cross
            SetLive(17, 0);
            SetLive(18, 0);
            SetLive(19, 0);
        }
    }
}
