﻿using ColossalFramework;
using ColossalFramework.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TrafficReport
{
    class TrafficAnalyzer 
    {
        public struct Report
        {
            public List<Vector3[]> paths;
        }


        VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
        NetManager netMan = Singleton<NetManager>.instance;
        QueryTool tool;
        bool working;

        public TrafficAnalyzer(QueryTool _tool)
        {
            working = false;
            tool = _tool;
        }

        public void ReportOnVehicle(ushort id)
        {
            if (working)
            {
                Log.warn("Job in progress bailing!");
                return;
            }

            working = true;

            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    Vehicle vehicle = vehicleManager.m_vehicles.m_buffer[id];

                    Log.info("Vechicle Name: " + vehicle.Info.name);

                    Vector3[] path = this.GatherPathVerticies(vehicle.m_path);

                    this.DumpPath(path);


                    working = false;
                    ThreadHelper.dispatcher.Dispatch(() => tool.OnGotVehiclePath(path));

                }
                catch (Exception e)
                {
                    Log.error(e.Message);
                    Log.error(e.StackTrace);
                }
            });
            
        }

        public void ReportOnSegment(ushort segmentID)
        {
            if (working)
            {
                Log.warn("Job in progress bailing!");
                return;
            }

            working = true;

            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try //
                {
                    Report report = this.DoReportOnSegment(segmentID);
                    working = false;
                    ThreadHelper.dispatcher.Dispatch(() => tool.OnGotSegmentReport(report));
                }
                catch (Exception e)
                {
                    Log.error(e.Message);
                    Log.error(e.StackTrace);
                }                
            });
        }

        public void ReportOnBuilding(ushort buildingID)
        {
            if (working)
            {
                Log.warn("Job in progress bailing!");
                return;
            }

            working = true;

            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    Report report = this.DoReportOnBuilding(buildingID);
                    working = false;
                    ThreadHelper.dispatcher.Dispatch(() => tool.OnGotBuildingReport(report));
                }
                catch (Exception e)
                {
                    Log.error(e.Message);
                    Log.error(e.StackTrace);
                }
            });
        }

        private Report DoReportOnSegment(ushort segmentID)
        {
            Report report;
            report.paths = new List<Vector3[]>();

            Vehicle[] vehicles = vehicleManager.m_vehicles.m_buffer;

            foreach (Vehicle vehicle in vehicles)
            {

                if ((vehicle.m_flags & Vehicle.Flags.Deleted) != Vehicle.Flags.None)
                {
                    continue;
                }

                Log.debug("Analyzing vehicle" + vehicle.Info.GetLocalizedDescriptionShort());

                if (vehicle.m_path == 0)
                {
                    continue;
                }

                Log.info("Vehcile valid, checking if path intersects segment...");

                if (this.PathContainsSegment(vehicle.m_path, segmentID))
                {
                    Log.info("Found vehicle on segemnt, getting path....");
                    Vector3[] path = this.GatherPathVerticies(vehicle.m_path);
                    report.paths.Add(path);
                    Log.info("Got Path");
                }
            }
                        

            Log.debug("End DoReportOnSegment");

            return report;
        }

        private Report DoReportOnBuilding(ushort buildingID)
        {
            Report report;
            report.paths = new List<Vector3[]>();

            Vehicle[] vehicles = vehicleManager.m_vehicles.m_buffer;

            foreach (Vehicle vehicle in vehicles)
            {

                if ((vehicle.m_flags & Vehicle.Flags.Deleted) != Vehicle.Flags.None)
                {
                    continue;
                }

                Log.debug("Analyzing vehicle" + vehicle.Info.GetLocalizedDescriptionShort());

                if (vehicle.m_path == 0)
                {
                    continue;
                }

                Log.info("Vehcile valid, checking if path intersects segment...");

              
                if(vehicle.m_sourceBuilding == buildingID || vehicle.m_targetBuilding == buildingID) {

                    Log.info("Found vehicle associated with building, getting path....");
                    Vector3[] path = this.GatherPathVerticies(vehicle.m_path);
                    report.paths.Add(path);
                    Log.info("Got Path");
                }
            }

            Log.debug("End DoReportOnSegment");

            return report;
        }

        private bool PathContainsSegment(uint pathID, ushort segmentID)
        {
            PathUnit path = this.getPath(pathID);
           
            while (true)
            {
                for (int i = 0; i < path.m_positionCount; i++)
                {
                    PathUnit.Position p = path.GetPosition(i);
                    if (p.m_segment == segmentID)
                    {
                        return true;
                    }
                }

                if (path.m_nextPathUnit == 0)
                {
                    return false;
                }
                path = this.getPath(path.m_nextPathUnit);
            }
        }

        PathUnit getPath(uint id)
        {
            return Singleton<PathManager>.instance.m_pathUnits.m_buffer[id];
        }

        Vector3[] GatherPathVerticies(uint pathID)
        {
            List<Vector3> verts = new List<Vector3>();

            Log.debug("Gathering path...");

            PathUnit path = this.getPath(pathID);
            NetSegment segment = netMan.m_segments.m_buffer[path.GetPosition(0).m_segment];
            NetNode startNode, endNode;
            Vector3 lastPoint;
            startNode = netMan.m_nodes.m_buffer[segment.m_startNode];
            lastPoint = startNode.m_position;
            //verts.Add(lastPoint);
            while (true)
            {
                for (int i = 0; i < path.m_positionCount; i++)
                {
                    PathUnit.Position p = path.GetPosition(i);

                    if (p.m_segment != 0)
                    {
                        segment = netMan.m_segments.m_buffer[p.m_segment];

                        startNode = netMan.m_nodes.m_buffer[segment.m_startNode];
                        endNode = netMan.m_nodes.m_buffer[segment.m_endNode];


                        Vector3 startPos = startNode.m_position;// +(Vector3.Cross(Vector3.up, segment.m_startDirection) * 5.0f); 
                        Vector3 endPos = endNode.m_position;// +(Vector3.Cross(Vector3.up, segment.m_endDirection) * -5.0f);

                        Vector3 direction = (endPos - startPos);

                        verts.Add(startPos + direction * ((float)p.m_offset / 255.0f)); 
                       // verts.Add(endPos);
                        //List<Vector3> segmentVerts = new List<Vector3>();

                        //verts.Add(startNode.m_position);
                        /*
                        if (!NetSegment.IsStraight(startNode.m_position, segment.m_startDirection, endNode.m_position, segment.m_endDirection))
                        {
                            Vector3 mp1, mp2;
                            NetSegment.CalculateMiddlePoints(
                                    startNode.m_position, segment.m_startDirection, 
                                    endNode.m_position, segment.m_endDirection, 
                                    true, true, out mp1, out mp2);
                            verts.Add(mp1);
                            verts.Add(mp2);
                        }*/
                        //verts.Add(endNode.m_position);
                    }
                }

                if (path.m_nextPathUnit == 0)
                {
                    Log.debug("Done");
                    return verts.ToArray();
                }
                path = this.getPath(path.m_nextPathUnit);
            }
        }

        void DumpPath(Vector3[] path)
        {
            string filename = ResourceLoader.BaseDir + "path.txt";
            Log.debug("Dumping path to " + filename);

            StreamWriter fs = new StreamWriter(filename);
            for (int i = 0; i < path.Length; i++)
            {
                fs.WriteLine(path[i].x + " " + path[i].y + " " + path[i].z);
            }
            fs.Close();
        }

    }
}
