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
        int m_xCells;
        int m_yCells;
        float m_cellSpacing; //Space in meters between cell positions
        bool m_naturalAppearance; //Whether the plants are placed in neat rows or randomized a bit
        string m_configPath; //Url path to community config settings

        SceneObjectGroup[,] m_prims; //list of objects managed by this module
        int[,] m_cellStatus; // plant type for each cell (0-3)
        string[] m_omvTrees = new string[22] {"None", "Pine1", "Pine2", "WinterPine1", "WinterPine2", "Oak", "TropicalBush1", "TropicalBush2", "Palm1", "Palm2", "Dogwood", "Cypress1", "Cypress2", "Plumeria", "WinterAspen", "Eucalyptus", "Fern", "Eelgrass", "SeaSword", "BeachGrass1", "Kelp1", "Kelp2"};

        int[] m_communityMembers = new int[6] {0, 1, 2, 5, 16, 18};//Default plants to include in the community
        bool m_isRunning = false; //Keep track of whether the automaton is running
        bool m_isSetup = false; //Whether the community matrix has been setup since the last region restart
        Timer m_cycleTimer = new Timer(); //Timer to replace the region heartbeat
        Timer m_pauseTimer = new Timer(); //Timer to delay trying to delete objects before the region has loaded completely
        Scene m_scene;
        Random m_random = new Random(); //A Random Class object to use throughout this module
        //Replacement Matrix.  The probability of replacement of one species by a surrounding species.
        //Example [0,1] is the probability that species 1 will be replace by species 0, if all 8 of species 1's neighbors are species 0.
        //From Thorhallsdottir 1990 as presented by Silvertown et al 1992.
        float[,] m_replacementMatrix = new float[6,6] {{0.00f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f},
                                                       {0.00f, 0.00f, 0.02f, 0.06f, 0.05f, 0.03f},
                                                       {0.00f, 0.23f, 0.00f, 0.09f, 0.32f, 0.37f},
                                                       {0.00f, 0.06f, 0.08f, 0.00f, 0.16f, 0.09f},
                                                       {0.00f, 0.44f, 0.06f, 0.06f, 0.00f, 0.11f},
                                                       {0.00f, 0.03f, 0.02f, 0.03f, 0.05f, 0.00f}};
        int[,] m_startingMatrix; //Starting values for the community

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
                m_cellSpacing = vMeadowConfig.GetFloat("cell_spacing", 2);
                m_naturalAppearance = vMeadowConfig.GetBoolean("natural_appearance", true);
                m_configPath = vMeadowConfig.GetString("config_path", "http://fernseed.usu.edu/vMeadowInfo/");
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
                m_pauseTimer.Elapsed += new ElapsedEventHandler(OnPause);
                m_pauseTimer.Interval = 30000;
                m_prims = new SceneObjectGroup[m_xCells, m_yCells];
                m_cellStatus = new int[m_xCells, m_yCells];
                RandomizeStartMatrix();
                m_pauseTimer.Start(); //Don't allow users to setup or use module til all objects have time to load from datastore
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

        void RandomizeStartMatrix()
        {
            //Generate starting matrix of random plant types
            m_startingMatrix = new int[m_xCells, m_yCells];
            for (int x=0; x<m_xCells; x++)
            {
                for (int y=0; y<m_yCells; y++)
                {
                    m_startingMatrix[x,y] = m_random.Next(6);
                }
            }
        }

        void SetupMatrix(bool hardReset)
        {
            if (hardReset)
            {
                //Remove any untracked plants that may be present after region restart and create new plants based on the starting matrix
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
                // Place plants in world based on the starting matrix
                m_cellStatus = new int[m_xCells, m_yCells];
                m_prims = new SceneObjectGroup[m_xCells, m_yCells];
                for (int x=0; x<m_xCells; x++)
                {
                    for (int y=0; y<m_yCells; y++)
                    {
                        int plantType = m_startingMatrix[x, y];
                        m_prims[x, y] = CreatePlant(x, y, plantType);
                        if (m_prims[x, y] != null)
                        {
                            m_cellStatus[x, y] = plantType;
                        }
                        else
                        {
                            //No plant in this cell
                            m_cellStatus[x, y] = 0;
                        }
                    }
                }
            }
            else
            {
                //Just replace any plants that need to change based on the starting matrix
                for (int x=0; x<m_xCells; x++)
                {
                    for (int y=0; y<m_yCells; y++)
                    {
                        //Generate a random plant type
                        int plantType = m_startingMatrix[x, y];
                        //Only replace the old one if it will be different than what is already there
                        if (plantType != m_cellStatus[x, y])
                        {
                            if (m_prims[x, y] != null)
                            {
                                DeletePlant(m_prims[x, y]);
                            }
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
        }

        void ClearAllPlants()
        {
            //Delete all vMeadow plants in the region
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
            m_isSetup = false;
        }

        SceneObjectGroup CreatePlant(int xPos, int yPos, int plantTypeIndex)
        {
            if (plantTypeIndex == 0)
            {
                return null;
            }
            else
            {
                float xRandomOffset = 0;
                float yRandomOffset = 0;
                if (m_naturalAppearance)
                {
                    xRandomOffset = ((float)m_random.NextDouble() - 0.5f) * m_cellSpacing;
                    yRandomOffset = ((float)m_random.NextDouble() - 0.5f) * m_cellSpacing;
                }
                Vector3 position = new Vector3(m_xPosition + (xPos * m_cellSpacing) + xRandomOffset, m_yPosition + (yPos * m_cellSpacing) + yRandomOffset, 0.0f);
                //Only calculate ground level if the x,y position is within the region boundaries
                if ((position.X >= 0) && (position.X <= 256) && (position.Y >= 0) && (position.Y <=256))
                {
                    position.Z = GroundLevel(position);
                    //Only add a plant if it is above sea level
                    if (position.Z >= WaterLevel(position))
                    {
                        Tree treeType = (Tree) Enum.Parse(typeof(Tree), m_omvTrees[m_communityMembers[plantTypeIndex]]);
                        SceneObjectGroup newPlant = AddTree(UUID.Zero, UUID.Zero, new Vector3(1.0f, 1.0f, 1.0f), Quaternion.Identity, position, treeType, false);
                        newPlant.RootPart.Name = "vMeadowPlant";
                        return newPlant;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        void DeletePlant(SceneObjectGroup deadPlant)
        {
            try
            {
                m_scene.DeleteSceneObject(deadPlant, false);
            }
            catch
            {
                m_log.Info("[vMeadow] Couldn't delete plant! May have been manually removed...");
            }
        }

        void StopAutomata()
        {
            m_log.Info("[vMeadow] Stopping...");
            m_isRunning = false;
            m_cycleTimer.Stop();
        }

        void StartAutomata()
        {
            //Start timer
            m_log.Info("[vMeadow] Starting...");
            m_isRunning = true;
            m_cycleTimer.Start();
        }

        void OnChat(Object sender, OSChatMessage chat)
        {
            if (chat.Channel != m_channel)
            {
                //Message is not for this module
                return;
            }
            else if (chat.Message.ToLower() == "reset")
            {
            if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("vMeadow Module: Resetting community.  Please be patient...");
                }
                if (m_isSetup)
                {
                    if (m_isRunning)
                    {
                        StopAutomata();
                    }
                    SetupMatrix(false);
                }
                else
                {
                    SetupMatrix(true);
                    m_isSetup = true;
                }
            }
            else if (chat.Message.ToLower() == "start")
            {
                if (m_isSetup)
                {
                    if (!m_isRunning)
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
                else
                {
                    if (m_dialogmod != null)
                    {
                        m_dialogmod.SendGeneralAlert("vMeadow Module: Cannot start.  Please reset the matrix first...");
                    }
                    m_log.Info("[vMeadow] Cannot start.  Matrix not setup...");
                }
            }
            else if (chat.Message.ToLower() == "stop")
            {
                if (m_isRunning)
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
            else if (chat.Message.ToLower() == "clear")
            {
                if (m_isRunning)
                {
                    StopAutomata();
                }
                if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("vMeadow Module: Clearing all plants.  Please be patient..");
                }
                m_log.Info("[vMeadow] Clearing all plants...");
                ClearAllPlants();
            }
            else if (chat.Message == "random")
            {
            if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("vMeadow Module: Randomizing...");
                }
                if (m_isRunning)
                {
                    StopAutomata();
                }
                RandomizeStartMatrix();
                SetupMatrix(!m_isSetup);
            }
            else
            {
                //Try to read configuration info from a url
                m_log.Info("[vMeadow] Loading configuration info...");
                bool readSuccess = ReadConfigs(System.IO.Path.Combine(m_configPath, "data?id=" + chat.Message));
                if (readSuccess)
                {
                    if (m_isRunning)
                    {
                        StopAutomata();
                    }
                    SetupMatrix(true);
                    m_isSetup = true;
                }
            }
        }

        bool ReadConfigs(string url)
        {
            string[] configInfo = new string[58]; //TODO: Could I import easier using xml instead of raw text?
            WebRequest configUrl = WebRequest.Create(url);
            if (m_dialogmod != null)
            {
                m_dialogmod.SendGeneralAlert("Reading data from url.  Please be patient...");
            }
            try
            {
                StreamReader urlData = new StreamReader(configUrl.GetResponse().GetResponseStream());
                string line;
                int lineCount = 0;
                while ((line = urlData.ReadLine()) != null)
                {
                    //Chop off the <br> at the end of the line
                    //TODO: What if there is not <br>?
                    configInfo[lineCount] = line.Substring(0, line.LastIndexOf("<"));
                    lineCount++;
                }
                //Parse the data
                string[] matrixInfo = new string[6];
                matrixInfo = configInfo[0].Split(',');
                m_xCells = Int32.Parse(matrixInfo[0]);
                m_yCells = Int32.Parse(matrixInfo[1]);
                m_xPosition = float.Parse(matrixInfo[2]);
                m_yPosition = float.Parse(matrixInfo[3]);
                m_cellSpacing = float.Parse(matrixInfo[4]);
                if (matrixInfo[5] == "1")
                {
                    m_naturalAppearance = true;
                }
                else
                {
                    m_naturalAppearance = false;
                }
                string[] plants = new string[5];
                plants = configInfo[1].Split(',');
                for (int i = 1; i<6; i++) //Start at index 1 so index 0 stays "None"
                {
                    m_communityMembers[i] = Int32.Parse(plants[i - 1]);
                }
                for (int i=0; i<6; i++)
                {
                    string[] probabilities = new string[6];
                    probabilities = configInfo[i + 2].Split(',');
                    for(int j=0; j<6; j++)
                    {
                        m_replacementMatrix[i,j] = float.Parse(probabilities[j]);
                    }
                }
                m_startingMatrix = new int[m_xCells, m_yCells];
                for (int i=0; i<m_xCells; i++)
                {
                    char[] startingPlants = new char[m_yCells];
                    startingPlants = configInfo[i + 8].ToCharArray();
                    for (int j=0; j<m_yCells; j++)
                    {
                        if (startingPlants[j] == 'R')
                        {
                            //Randomly select a plant type
                            m_startingMatrix[i,j] = m_random.Next(6);
                        }
                        else
                        {
                            m_startingMatrix[i,j] = Int32.Parse(startingPlants[j].ToString());
                        }
                    }
                }
                if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("vMeadow Module: Read parameters from url \"" + url + "\".  Clearing all plants and generating a new community.  Please be patient...");
                }
                m_log.Info("[vMeadow] Read parameters from url \"" + url + "\"...");

                return true;
            }
            catch //failed to get the data for some reason
            {
                m_log.Error("[vMeadow] Error loading parameters from url \"" + url + "\"...");
                if (m_dialogmod != null)
                {
                    m_dialogmod.SendGeneralAlert("vMeadow Module: Error loading parameters from url \"" + url + "\"...");
                }
                return false;
            }
        }

        void OnCycleTimer(object source, ElapsedEventArgs e)
        {
            //Advance the cellular automata by a generation
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
                    for (int neighborSpecies=0; neighborSpecies<6; neighborSpecies++)
                    {
                        replacementProbability[neighborSpecies] = m_replacementMatrix[neighborSpecies, currentSpecies] * ((float)neighborSpeciesCounts[neighborSpecies] / 8.0f);
                    }
                    //Randomly determine the new species based on the replacement probablilities
                    float randomReplacement = (float)m_random.NextDouble();
                    int newStatus;
                    if (randomReplacement <= replacementProbability[0])
                    {
                        newStatus = 0;
                    }
                    if (randomReplacement <= replacementProbability[1] + replacementProbability[0])
                    {
                        newStatus = 1;
                    }
                    else if (randomReplacement <= replacementProbability[2] + replacementProbability[1] + replacementProbability[0])
                    {
                        newStatus = 2;
                    }
                    else if (randomReplacement <= replacementProbability[3] + replacementProbability[2] + replacementProbability[1] + replacementProbability[0])
                    {
                        newStatus = 3;
                    }
                    else if (randomReplacement <= replacementProbability[4] + replacementProbability[3] + replacementProbability[2] + replacementProbability[1] + replacementProbability[0])
                    {
                        newStatus = 4;
                    }
                    else if (randomReplacement <= replacementProbability[5] + replacementProbability[4] + replacementProbability[3] + replacementProbability[2] + replacementProbability[1] + replacementProbability[0])
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
                        if (oldCellStatus[x, y] != 0)
                        {
                            //Don't try to delete plants that don't exist
                            DeletePlant(m_prims[x, y]);
                        }
                        m_prims[x, y] = CreatePlant(x, y, newStatus);
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
            //After region has had time to load all objects from database after a restart...
            m_pauseTimer.Stop();
            m_scene.EventManager.OnChatFromWorld += new EventManager.ChatFromWorldEvent(OnChat);
            m_scene.EventManager.OnChatFromClient += new EventManager.ChatFromClientEvent(OnChat);
            m_cycleTimer.Elapsed += new ElapsedEventHandler(OnCycleTimer);
            m_cycleTimer.Interval = m_cycleTime;
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
