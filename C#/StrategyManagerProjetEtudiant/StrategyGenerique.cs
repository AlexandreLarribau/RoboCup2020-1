﻿using Constants;
using EventArgsLibrary;
using HeatMap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Utilities;
using WorldMap;

namespace StrategyManagerProjetEtudiantNS
{
    /****************************************************************************/
    /// <summary>
    /// Il y a un Strategy Manager par robot, qui partage la même Global World Map -> les stratégies collaboratives sont possibles
    /// Le Strategy Manager a pour rôle de déterminer les déplacements et les actions du robot auquel il appartient
    /// 
    /// Il implante implante à minima le schéma de fonctionnement suivant
    /// - Récupération asynchrone de la Global World Map décrivant l'état du monde autour du robot
    ///     La Global World Map inclus en particulier l'état du jeu (à voir pour changer cela)
    /// - Sur Timer Strategy : détermination si besoin du rôle du robot :
    ///         - simple si Eurobot car les rôles sont figés
    ///         - complexe dans le cas de la RoboCup car les rôles sont changeant en fonction des positions et du contexte.
    /// - Sur Timer Strategy : Itération des machines à état de jeu définissant les déplacements et actions
    ///         - implante les machines à état de jeu à Eurobot, ainsi que les règles spécifiques 
    ///         de jeu (déplacement max en controlant le ballon par exemple à la RoboCup).
    ///         - implante les règles de mise à jour 
    ///             des zones préférentielles de destination (par exemple la balle pour le joueur qui la conteste à la RoboCup), 
    ///             des zones interdites (par exemple les zones de départ à Eurobot), d
    ///             es zones à éviter (par exemple pour se démarquer à la RoboCup)...
    /// - DONE - Sur Timer Strategy : génération de la HeatMap de positionnement X Y donnant l'indication d'intérêt de chacun des points du terrain
    ///     et détermination de la destination théorique (avant inclusion des masquages waypoint)
    /// - DONE - Sur Timer Strategy : prise en compte de la osition des obstacles pour générer la HeatMap de WayPoint 
    ///     et trouver le WayPoint courant.
    /// - Sur Timer Strategy : gestion des actions du robot en fonction du contexte
    ///     Il est à noter que la gestion de l'orientation du robot (différente du cap en déplacement de celui-ci)
    ///     est considérée comme une action, et non comme un déplacement car celle-ci dépend avant tout du contexte du jeu
    ///     et non pas de la manière d'aller à un point.
    /// </summary>

    /****************************************************************************/
    public abstract class StrategyGenerique
    {
        public int robotId = 0;
        public int teamId = 0;
        public string teamIpAddress = "";
        public string DisplayName;

        public GlobalWorldMap globalWorldMap;
        public Location robotCurrentLocation = new Location(0, 0, 0, 0, 0, 0);
        public double robotOrientation;

        
        System.Timers.Timer timerStrategy;

        public StrategyGenerique(int robotId, int teamId, string teamIpAddress)
        {
            this.teamId = teamId;
            this.robotId = robotId;
            this.teamIpAddress = teamIpAddress;

            globalWorldMap = new GlobalWorldMap();

            timerStrategy = new System.Timers.Timer();
            timerStrategy.Interval = 50;
            timerStrategy.Elapsed += TimerStrategy_Elapsed;
            timerStrategy.Start();
        }

        public abstract void InitStrategy();

        //************************ Events reçus ************************************************/
        //public abstract void OnRefBoxMsgReceived(object sender, WorldMap.RefBoxMessageArgs e);

        /// Evènement envoyé par le module de gestion de la LocalWorldMap
        public void OnGlobalWorldMapReceived(object sender, GlobalWorldMapArgs e)
        {
            //On récupère la nouvelle worldMap
            lock (globalWorldMap)
            {
                globalWorldMap = e.GlobalWorldMap;
            }
        }

        /// Evènement envoyé par le module de calcul de Positioning
        public void OnPositionRobotReceived(object sender, LocationArgs location)
        {

            robotCurrentLocation.X = location.Location.X;
            robotCurrentLocation.Y = location.Location.Y;
            robotCurrentLocation.Theta = location.Location.Theta;

            robotCurrentLocation.Vx = location.Location.Vx;
            robotCurrentLocation.Vy = location.Location.Vy;
            robotCurrentLocation.Vtheta = location.Location.Vtheta;
        }

        private void TimerStrategy_Elapsed(object sender, ElapsedEventArgs e)
        {
            IterateStateMachines();
            //Mise à jour de l'affichage de la world map
            OnUpdateWorldMapDisplay(robotId);

        }

        public abstract void IterateStateMachines(); //A définir dans les classes héritées


        /****************************************** Events envoyés ***********************************************/

        public event EventHandler<LocationArgs> OnDestinationEvent;
        public virtual void OnDestination(int id, Location location)
        {
            OnDestinationEvent?.Invoke(this, new LocationArgs { RobotId = id, Location = location });
        }

        public EventHandler<EventArgs> OnUpdateWorldMapDisplayEvent;
        public virtual void OnUpdateWorldMapDisplay(int id)
        {
            var handler = OnUpdateWorldMapDisplayEvent;
            if (handler != null)
            {
                handler(this, new EventArgs());
            }
        }

        public event EventHandler<PolarPIDSetupArgs> On2WheelsPolarSpeedPIDSetupEvent;
        public virtual void On2WheelsPolarSpeedPIDSetup(double px, double ix, double dx, double ptheta, double itheta, double dtheta,
            double pxLimit, double ixLimit, double dxLimit, double pthetaLimit, double ithetaLimit, double dthetaLimit
            )
        {
            On2WheelsPolarSpeedPIDSetupEvent?.Invoke(this, new PolarPIDSetupArgs
            {
                P_x = px,
                I_x = ix,
                D_x = dx,
                P_theta = ptheta,
                I_theta = itheta,
                D_theta = dtheta,
                P_x_Limit = pxLimit,
                I_x_Limit = ixLimit,
                D_x_Limit = dxLimit,
                P_theta_Limit = pthetaLimit,
                I_theta_Limit = ithetaLimit,
                D_theta_Limit = dthetaLimit
            });
        }

        public event EventHandler<LidarMessageArgs> OnMessageEvent;
        public virtual void OnLidarMessage(string message, int line)
        {
            OnMessageEvent?.Invoke(this, new LidarMessageArgs { Value = message, Line = line });
        }
        
        public event EventHandler<IndependantPIDSetupArgs> On2WheelsIndependantSpeedPIDSetupEvent;
        public virtual void On2WheelsIndependantSpeedPIDSetup(double pM1, double iM1, double dM1, double pM2, double iM2, double dM2,
            double pM1Limit, double iM1Limit, double dM1Limit, double pM2Limit, double iM2Limit, double dM2Limit)
        {
            On2WheelsIndependantSpeedPIDSetupEvent?.Invoke(this, new IndependantPIDSetupArgs
            {
                P_M1 = pM1,
                I_M1 = iM1,
                D_M1 = dM1,
                P_M2 = pM2,
                I_M2 = iM2,
                D_M2 = dM2,
                P_M1_Limit = pM1Limit,
                I_M1_Limit = iM1Limit,
                D_M1_Limit = dM1Limit,
                P_M2_Limit = pM2Limit,
                I_M2_Limit = iM2Limit,
                D_M2_Limit = dM2Limit,
            });
        }

        public event EventHandler<ByteEventArgs> OnSetAsservissementModeEvent;
        public virtual void OnSetAsservissementMode(byte val)
        {
            OnSetAsservissementModeEvent?.Invoke(this, new ByteEventArgs { Value = val });
        }

        public event EventHandler<SpeedConsigneToMotorArgs> OnSetSpeedConsigneToMotor;
        public virtual void OnSetSpeedConsigneToMotorEvent(object sender, SpeedConsigneToMotorArgs e)
        {
            OnSetSpeedConsigneToMotor?.Invoke(sender, e);
        }

        public event EventHandler<BoolEventArgs> OnEnableDisableMotorCurrentDataEvent;
        public virtual void OnEnableDisableMotorCurrentData(bool val)
        {
            OnEnableDisableMotorCurrentDataEvent?.Invoke(this, new BoolEventArgs { value = val });
        }

        public event EventHandler<CollisionEventArgs> OnCollisionEvent;
        public virtual void OnCollision(int id, Location robotLocation)
        {
            OnCollisionEvent?.Invoke(this, new CollisionEventArgs { RobotId = id, RobotRealPositionRefTerrain = robotLocation });
        }

        public event EventHandler<IOValuesEventArgs> OnIOValuesFromRobotEvent;
        public void OnIOValuesFromRobot(object sender, IOValuesEventArgs e)
        {
            OnIOValuesFromRobotEvent?.Invoke(sender, e);
        }

        public event EventHandler<DoubleEventArgs> OnOdometryPointToMeterSetupEvent;
        public void OnOdometryPointToMeter(double value)
        {
            OnOdometryPointToMeterSetupEvent?.Invoke(this, new DoubleEventArgs { Value = value });
        }

        public event EventHandler<TwoWheelsAngleArgs> On2WheelsAngleSetupEvent;
        public void On2WheelsAngleSetup(double angleM1, double angleM2)
        {
            On2WheelsAngleSetupEvent?.Invoke(this, new TwoWheelsAngleArgs { angleMotor1 = angleM1, angleMotor2 = angleM2});
        }

        public event EventHandler<TwoWheelsToPolarMatrixArgs> On2WheelsToPolarMatrixSetupEvent;
        public void On2WheelsToPolarSetup(double mX1, double mX2, double mTheta1, double mTheta2)
        {
            On2WheelsToPolarMatrixSetupEvent?.Invoke(this, new TwoWheelsToPolarMatrixArgs
            {
                mx1 = mX1,
                mx2 = mX2,
                mtheta1 = mTheta1,
                mtheta2 = mTheta2,
            });
        }

        public event EventHandler<StringEventArgs> OnTextMessageEvent;
        public virtual void OnTextMessage(string str)
        {
            var handler = OnTextMessageEvent;
            if (handler != null)
            {
                handler(this, new StringEventArgs { value = str });
            }
        }
    }    
}
