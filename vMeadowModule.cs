/*
 * Copyright (c) Contributors http://github.com/aduffy70/vMeadow
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the vMeadow Module nor the
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

[assembly: Addin("vMeadowModule", "0.1")]
[assembly: AddinDependency("OpenSim", "0.5")]
namespace vMeadowModule
{
    [Extension(Path="/OpenSim/RegionModules",NodeName="RegionModule")]
    public class vMeadowModule : INonSharedRegionModule, IVegetationModule
    {
        //Set up logging and dialog messages
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        IDialogModule m_dialogmod;
        //Configurable settings
        bool m_enabled = false;
        int m_channel;  //Channel for chat commands
        int m_cycleTime; //Time in milliseconds between cycles
        float m_xPosition; //inworld x coordinate for the 0,0 cell position
        float m_yPosition;  //inworld y coordinate for the 0,0 cell position
        float m_zPosition = 40; //TODO: Needs to be ground level at the XY coordinates
        int m_xCells;
        int m_yCells;
        float m_cellSpacing; //Space in meters between cell positions

        SceneObjectGroup[,] m_prims; //list of objects managed by this module
        int[,] m_cellStatus; // plant type for each cell (0-3)
        string[] m_treeTypes = new string[6] {"None", "Eelgrass", "Fern", "BeachGrass1", "SeaSword", "TropicalBush1"};
        bool m_running = false; //Keep track of whether the automaton is running
        Timer m_cycleTimer = new Timer(); //Timer to replace the region heartbeat
        Timer m_pauseTimer = new Timer(); //Timer to delay trying to delete objects before the region has loaded completely
        Scene m_scene;
        Random m_random = new Random(); //A Random Class object to use throughout this module
        //Replacement Matrix.  The probability of replacement of species y by species x.
        //From Thorhallsdottir 1990 as presented by Silvertown et al 1992.
        float[,] m_replacementMatrix = new float[6,6] {{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f},
                                                       {0.00f, 0.00f, 0.02f, 0.06f, 0.05f, 0.03f},
                                                       {0.00f, 0.23f, 0.00f, 0.09f, 0.32f, 0.37f},
                                                       {0.00f, 0.06f, 0.08f, 0.00f, 0.16f, 0.09f},
                                                       {0.00f, 0.44f, 0.06f, 0.06f, 0.00f, 0.11f},
                                                       {0.00f, 0.03f, 0.02f, 0.03f, 0.05f, 0.00f}};
        //int m_deathCount = 0; //TEMP: track deaths to see if we will have the overflow problem @~16k

        #region INonSharedRegionModule interface

        public void Initialise(IConfigSource config)
        {
            IConfig vMeadowConfig = config.Configs["vMeadow"];
            if (vMeadowConfig != null)
            {
                m_enabled = vMeadowConfig.GetBoolean("enabled", false);
                m_cycleTime = vMeadowConfig.GetInt("cycle_time", 10) * 1000;
                m_channel = vMeadowConfig.GetInt("chat_channel", 18);
                m_xCells = vMeadowConfig.GetInt("x_cells", 36);
                m_yCells = vMeadowConfig.GetInt("y_cells", 36);
                m_xPosition = vMeadowConfig.GetFloat("x_position", 128);
                m_yPosition = vMeadowConfig.GetFloat("y_position", 128);
                m_zPosition = vMeadowConfig.GetFloat("z_position", 40);
                m_cellSpacing = vMeadowConfig.GetFloat("cell_spacing", 2);
            }
            if (m_enabled)
            {
                m_log.Info("[vMeadow] Initializing...");
            }
        }

        public void AddRegion(Scene scene)
        {
            if (m_enabled)
            {
                m_scene = scene;
                m_dialogmod = m_scene.RequestModuleInterface<IDialogModule>();
                m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
                m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
                m_cycleTimer.Elapsed += new ElapsedEventHandler(OnCycleTimer);
                m_cycleTimer.Interval = m_cycleTime;
                m_pauseTimer.Elapsed += new ElapsedEventHandler(OnPause);
                m_pauseTimer.Interval = 60000;
                m_prims = new SceneObjectGroup[m_xCells, m_yCells];
                m_cellStatus = new int[m_xCells, m_yCells];
                m_pauseTimer.Start(); //Don't run module til all objects have time to load from datastore

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
                return "vMeadowModule";
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

        #region IVegetationModule Members

        public SceneObjectGroup AddTree(UUID uuid, UUID groupID, Vector3 scale, Quaternion rotation, Vector3 position, Tree treeType, bool newTree)
        {
            PrimitiveBaseShape treeShape = new PrimitiveBaseShape();
            treeShape.PathCurve = 16;
            treeShape.PathEnd = 49900;
            treeShape.PCode = newTree ? (byte)PCode.NewTree : (byte)PCode.Tree;
            treeShape.Scale = scale;
            treeShape.State = (byte)treeType;
            return m_scene.AddNewPrim(uuid, groupID, position, rotation, treeShape);
        }


        #endregion

        #region IEntityCreator Members

        protected static readonly PCode[] creationCapabilities = new PCode[] { PCode.Grass, PCode.NewTree, PCode.Tree };

        public PCode[] CreationCapabilities
        {
            get
            {
                return creationCapabilities;
            }
        }

        public SceneObjectGroup CreateEntity(UUID ownerID, UUID groupID, Vector3 pos, Quaternion rot, PrimitiveBaseShape shape)
        {
            if (Array.IndexOf(creationCapabilities, (PCode)shape.PCode) < 0)
            {
                m_log.DebugFormat("[VEGETATION]: PCode {0} not handled by {1}", shape.PCode, Name);
                return null;
            }
            SceneObjectGroup sceneObject = new SceneObjectGroup(ownerID, pos, rot, shape);
            SceneObjectPart rootPart = sceneObject.GetChildPart(sceneObject.UUID);
            rootPart.AddFlag(PrimFlags.Phantom);
            m_scene.AddNewSceneObject(sceneObject, false);
            sceneObject.SetGroup(groupID, null);
            return sceneObject;
        }

        #endregion

        void SetupMatrix()
        {
            //Delete any plants already in the region
            EntityBase[] everyObject = m_scene.GetEntities();
            SceneObjectGroup sog;
            foreach (EntityBase e in everyObject)
            {
                if (e is SceneObjectGroup)
                {
                    sog = (SceneObjectGroup)e;
                    if (sog.RootPart.Name == "vMeadowPlant")
                    {
                        DeletePlant(sog);
                    }
                }
            }
            // Place a randomized grid of plants in world
            for (int x=0; x<m_xCells; x++)
            {
                for (int y=0; y<m_yCells; y++)
                {
                    //Generate a random plant
                    int plantType = m_random.Next(1, 6);
                    m_prims[x, y] = CreatePlant(x, y, plantType);
                    if (m_prims[x, y] != null)
                    {
                        m_cellStatus[x, y] = plantType;
                    }
                    else
                    {
                        //No plant in this cell (probably below sea level)
                        m_cellStatus[x, y] = 0;
                    }
                }
            }
            //Place the managed objects visibly in the scene
            foreach (SceneObjectGroup sogr in m_prims)
            {
                m_scene.AddNewSceneObject(sogr, false);
            }
        }

        SceneObjectGroup CreatePlant(int xPos, int yPos, int plantTypeIndex)
        {
            float xRandomOffset = ((float)m_random.NextDouble() - 0.5f) * m_cellSpacing;
            float yRandomOffset = ((float)m_random.NextDouble() - 0.5f) * m_cellSpacing;
            Vector3 position = new Vector3(m_xPosition + (xPos * m_cellSpacing) + xRandomOffset, m_yPosition + (yPos * m_cellSpacing) + yRandomOffset, 0.0f);
            position.Z = GroundLevel(position);
            //Only add a plant if it is above sea level
            if (position.Z >= WaterLevel(position))
            {
                Tree treeType = (Tree) Enum.Parse(typeof(Tree), m_treeTypes[plantTypeIndex]);
                SceneObjectGroup newPlant = AddTree(UUID.Zero, UUID.Zero, new Vector3(1.0f, 1.0f, 1.0f), Quaternion.Identity, position, treeType, false);
                newPlant.RootPart.Name = "vMeadowPlant";
                return newPlant;
            }
            else
            {
                return null;
            }
        }

        void DeletePlant(SceneObjectGroup deadPlant)
        {
            m_scene.DeleteSceneObject(deadPlant, false);
            //m_deathCount ++;
            //m_log.Info("[vMeadow] Death count: " + m_deathCount.ToString());
        }

        void RandomizeAllCells()
        {
            //Set each plant to a random plant type
            m_log.Info("[vMeadow] resetting...");
            for (int x=0; x<m_xCells; x++)
            {
                for (int y=0; y<m_yCells; y++)
                {
                    //Generate a random plant type
                    int plantType = m_random.Next(1, 6);
                    //Only replace the old one if it will be different than what is already there
                    if (plantType != m_cellStatus[x, y])
                    {
                        DeletePlant(m_prims[x, y]);
                        m_prims[x, y] = CreatePlant(x, y, plantType);
                        if (m_prims[x, y] != null)
                        {
                            m_cellStatus[x, y] = plantType;
                        }
                        else
                        {
                            m_cellStatus[x, y] = 0;
                        }
                    }
                }
            }
        }

        void StopAutomata()
        {
            m_log.Info("[vMeadow] Stopping...");
            m_running = false;
            m_cycleTimer.Stop();
        }

        void StartAutomata()
        {
            //Start timer
            m_log.Info("[vMeadow] Starting...");
            m_running = true;
            m_cycleTimer.Start();
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
                    StopAutomata();
                }
                if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("vMeadow Module: Reset...");
                }
                RandomizeAllCells();
            }
            else if (chat.Message == "start")
            {
                if (!m_running)
                {
                    if (m_dialogmod != null)
                    {
                        m_dialogmod.SendGeneralAlert("vMeadow Module: Start...");
                    }
                    StartAutomata();
                }
                else
                {
                    if (m_dialogmod != null)
                    {
                        m_dialogmod.SendGeneralAlert("vMeadow Module: Already running...");
                    }
                    m_log.Info("[vMeadow] Already running...");
                }
            }
            else if (chat.Message == "stop")
            {
                if (m_running)
                {
                    if (m_dialogmod != null)
                    {
                        m_dialogmod.SendGeneralAlert("vMeadow Module: Stop...");
                    }
                    StopAutomata();
                }
                else
                {
                    if (m_dialogmod != null)
                    {
                        m_dialogmod.SendGeneralAlert("vMeadow Module: Already stopped...");
                    }
                    m_log.Info("[vMeadow] Not running...");
                }
            }
            else
            {
                //invalid command
                if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("vMeadow Module: Invalid command...");
                }
                m_log.Info("[vMeadow] Invalid command...");
            }
        }

        void OnCycleTimer(object source, ElapsedEventArgs e)
        {
            int[,] oldCellStatus = new int[m_xCells, m_yCells];
            Array.Copy(m_cellStatus, oldCellStatus, m_cellStatus.Length);
            int rowabove;
            int rowbelow;
            int colleft;
            int colright;
            for (int x=0; x<m_xCells; x++)
            {
                if (x == 0)
                {
					colright = x + 1;
					colleft = m_xCells - 1;
				}
				else if (x == m_xCells - 1)
				{
					colright = 0;
					colleft = x - 1;
				}
				else
				{
					colright = x + 1;
					colleft = x - 1;
				}
				for (int y=0; y<m_yCells; y++)
				{
                    if (y == 0)
                    {
						rowabove = y + 1;
						rowbelow = m_yCells - 1;
					}
					else if (y == m_yCells - 1)
					{
						rowabove = 0;
						rowbelow = y - 1;
					}
					else
					{
						rowabove = y + 1;
						rowbelow = y - 1;
					}
                    float[] replacementProbability = new float[6];
                    int[] neighborSpeciesCounts = new int[6] {0, 0, 0, 0, 0, 0};
                    int currentSpecies = oldCellStatus[x, y];
                    //Get counts of neighborspecies
                    neighborSpeciesCounts[oldCellStatus[colleft, rowabove]]++;
                    neighborSpeciesCounts[oldCellStatus[x, rowabove]]++;
                    neighborSpeciesCounts[oldCellStatus[colright, rowabove]]++;
                    neighborSpeciesCounts[oldCellStatus[colleft, y]]++;
                    neighborSpeciesCounts[oldCellStatus[colright, y]]++;
                    neighborSpeciesCounts[oldCellStatus[colleft, rowbelow]]++;
                    neighborSpeciesCounts[oldCellStatus[x, rowbelow]]++;
                    neighborSpeciesCounts[oldCellStatus[colright, rowbelow]]++;
                    for (int neighborSpecies=1; neighborSpecies<6; neighborSpecies++)
                    {
                        replacementProbability[neighborSpecies] = m_replacementMatrix[neighborSpecies, currentSpecies] * ((float)neighborSpeciesCounts[neighborSpecies] / 8.0f);
                    }
                    //Randomly determine the new species based on the replacement probablilities
                    float randomReplacement = (float)m_random.NextDouble();
                    int newStatus;
                    if (randomReplacement <= replacementProbability[1])
                    {
                        newStatus = 1;
                    }
                    else if (randomReplacement <= replacementProbability[2] + replacementProbability[1])
                    {
                        newStatus = 2;
                    }
                    else if (randomReplacement <= replacementProbability[3] + replacementProbability[2] + replacementProbability[1])
                    {
                        newStatus = 3;
                    }
                    else if (randomReplacement <= replacementProbability[4] + replacementProbability[3] + replacementProbability[2] + replacementProbability[1])
                    {
                        newStatus = 4;
                    }
                    else if (randomReplacement <= replacementProbability[5] + replacementProbability[4] + replacementProbability[3] + replacementProbability[2] + replacementProbability[1])
                    {
                        newStatus = 5;
                    }
                    else
                    {
                        newStatus = oldCellStatus[x, y];
                    }
                    //Only delete and replace the plant if it will be different than what is already there
                    if (newStatus != oldCellStatus[x, y])
                    {
                        DeletePlant(m_prims[x, y]);
                        m_prims[x, y] = CreatePlant(x, y, m_cellStatus[x, y]);
                        if (m_prims != null)
                        {
                            m_cellStatus[x, y] = newStatus;
                        }
                        else
                        {
                            m_cellStatus[x, y] = 0;
                        }
                    }
                }
            }
        }

        void OnPause(object source, ElapsedEventArgs e)
        {
            m_pauseTimer.Stop();
            SetupMatrix();
        }

        float GroundLevel(Vector3 location)
        {
            //Return the ground level at the specified location.
            //The first part of this function performs essentially the same function as llGroundNormal() without having to be called by a prim.
            //Find two points in addition to the position to define a plane
            Vector3 p0 = new Vector3(location.X, location.Y, (float)m_scene.Heightmap[(int)location.X, (int)location.Y]);
            Vector3 p1 = new Vector3();
            Vector3 p2 = new Vector3();
            if ((location.X + 1.0f) >= m_scene.Heightmap.Width)
                p1 = new Vector3(location.X + 1.0f, location.Y, (float)m_scene.Heightmap[(int)location.X, (int)location.Y]);
            else
                p1 = new Vector3(location.X + 1.0f, location.Y, (float)m_scene.Heightmap[(int)(location.X + 1.0f), (int)location.Y]);
            if ((location.Y + 1.0f) >= m_scene.Heightmap.Height)
                p2 = new Vector3(location.X, location.Y + 1.0f, (float)m_scene.Heightmap[(int)location.X, (int)location.Y]);
            else
                p2 = new Vector3(location.X, location.Y + 1.0f, (float)m_scene.Heightmap[(int)location.X, (int)(location.Y + 1.0f)]);
            //Find normalized vectors from p0 to p1 and p0 to p2
            Vector3 v0 = new Vector3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3 v1 = new Vector3(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z);
            v0.Normalize();
            v1.Normalize();
            //Find the cross product of the vectors (the slope normal).
            Vector3 vsn = new Vector3();
            vsn.X = (v0.Y * v1.Z) - (v0.Z * v1.Y);
            vsn.Y = (v0.Z * v1.X) - (v0.X * v1.Z);
            vsn.Z = (v0.X * v1.Y) - (v0.Y * v1.X);
            vsn.Normalize();
            //The second part of this function does the same thing as llGround() without having to be called from a prim
            //Get the height for the integer coordinates from the Heightmap
            float baseheight = (float)m_scene.Heightmap[(int)location.X, (int)location.Y];
            //Calculate the difference between the actual coordinates and the integer coordinates
            float xdiff = location.X - (float)((int)location.X);
            float ydiff = location.Y - (float)((int)location.Y);
            //Use the equation of the tangent plane to adjust the height to account for slope
            return (((vsn.X * xdiff) + (vsn.Y * ydiff)) / (-1 * vsn.Z)) + baseheight;
        }

        float WaterLevel(Vector3 location)
        {
            //Return the water level at the specified location.
            //This function performs essentially the same function as llWater() without having to be called by a prim.
            return (float)m_scene.RegionInfo.RegionSettings.WaterHeight;
        }
    }
}
