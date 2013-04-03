using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Debridement
{
	class DebridementManager : MonoBehaviour
	{

		public float CLEANUP_INTERVAL = 5.0f;
		public float LANDED_CLEANUP_MIN_DELAY = 60.0f * 15.0f;
		public float LANDED_CLEANUP_DISTANCE_DELAY = 3600.0f * 24.0f;
		public float LANDED_CLEANUP_SPLASH_FACTOR = 2.0f;
		public float ORBIT_CLEANUP_MIN_DELAY = 3600.0f;
		public float ORBIT_CLEANUP_ATMOS_SECS = 4.0f;

		public float TIMESTEP = 1 / 5.0f;
		public double DRAG_FACTOR = 1.0;
		public double REF_SPEED = 2000.0;

		private bool controlDown = false;

		private bool kscRadialVectorFound = false;
		private Vector3d kscRadialVector = new Vector3d(1.0, 0.0, 0.0);

		//Singleton

		public static GameObject GameObjectInstance;

		//Methods

		public static bool isValidForLanded(Vessel vessel)
		{
			return !vessel.isCommandable && vessel.LandedOrSplashed && vessel.mainBody.bodyName == "Kerbin";
		}

		public static bool isValidForDecay(Vessel vessel)
		{
			return (!vessel.isCommandable
				&& vessel.situation == Vessel.Situations.ORBITING && vessel.mainBody.atmosphere
				&& vessel.orbit.PeA < vessel.mainBody.maxAtmosphereAltitude);
		}

		public static double getAtmosphericDensity(CelestialBody body, double alt) {

			if (body.atmosphere && alt < body.maxAtmosphereAltitude)
			{

				double atmosphere_scale_height = body.atmosphereScaleHeight * 1000;
				double atmosphere_alt_factor = Math.Exp((-alt) / atmosphere_scale_height);

				return body.atmosphereMultiplier * atmosphere_alt_factor;

			}

			return 0.0;

		}

		public static double getAtmosphereSecondsPerOrbit(Orbit orbit)
		{
			//Calculate the atmosphere densities at pe and ap
			double pe_density = getAtmosphericDensity(orbit.referenceBody, orbit.PeA);
			double ap_density = getAtmosphericDensity(orbit.referenceBody, orbit.ApA);

			//Calculate the average density
			double pe_factor = 1.0-Math.Sqrt(orbit.eccentricity);
			double average_density = pe_density * pe_factor + ap_density * (1.0-pe_factor);

			return average_density * orbit.period;
		}

		public static double getTotalAtmosphereSeconds(Vessel vessel) {

			double num_orbits = vessel.missionTime / vessel.orbit.period;

			return getAtmosphereSecondsPerOrbit(vessel.orbit) * num_orbits;
		}

		public double getDistanceFactorFromKSC(Vessel vessel)
		{
			double angle_from_ksc = Math.Acos(
				Vector3d.Dot((vessel.GetWorldPos3D() - vessel.mainBody.position).normalized, kscRadialVector)
				);

			return angle_from_ksc / Math.PI;
		}

		public double getDistanceFromKSC(Vessel vessel)
		{
			return getDistanceFactorFromKSC(vessel) * (Math.PI * vessel.mainBody.Radius);
		}

		public double getLandedCleanupTime(Vessel vessel)
		{
			double splash_factor = 1.0;

			if (vessel.situation == Vessel.Situations.SPLASHED)
				splash_factor = LANDED_CLEANUP_SPLASH_FACTOR;

			return Math.Max(LANDED_CLEANUP_MIN_DELAY, LANDED_CLEANUP_DISTANCE_DELAY * getDistanceFactorFromKSC(vessel)) * splash_factor;
		}

		public void cleanUpDebris()
		{

			if (FlightGlobals.ready && HighLogic.LoadedSceneIsFlight)
			{
				Queue<Vessel> delete_queue = new Queue<Vessel>();

				int candidate_landed = 0;
				int candidate_orbiting = 0;

				int deleted_landed = 0;
				int deleted_orbiting = 0;

				foreach (Vessel vessel in FlightGlobals.Vessels)
				{

					//Check if vessel is un-loaded debris
					if (!vessel.loaded && !vessel.isCommandable)
					{
						
						if (isValidForLanded(vessel))
						{

							candidate_landed++;

							if (vessel.missionTime > getLandedCleanupTime(vessel)) {
								//Delete the vessel if it has been landed for long enough
								delete_queue.Enqueue(vessel);
								deleted_landed++;
							}

						}
						else if (isValidForDecay(vessel))
						{
							//Delete debris orbiting within their body's atmosphere
							candidate_orbiting++;

							if (vessel.missionTime > ORBIT_CLEANUP_MIN_DELAY
								&& vessel.orbit.altitude < vessel.mainBody.maxAtmosphereAltitude)
							{
								//Determine how many atmosphere seconds the vessel has experienced
								if (getTotalAtmosphereSeconds(vessel) > ORBIT_CLEANUP_ATMOS_SECS) {
									//Delete the vessel if it has experienced enough atmosphere seconds
									delete_queue.Enqueue(vessel);
									deleted_orbiting++;
								}
							}
							
						}

					}
				}

				while (delete_queue.Count > 0)
				{
					Vessel vessel = delete_queue.Dequeue();
					vessel.Die();
				}

				/*
				if (candidate_orbiting > 0)
					Debug.Log("Found " + candidate_orbiting + " debris orbiting in-atmosphere");

				if (candidate_landed > 0)
					Debug.Log("Found " + candidate_landed + " debris landed at Kerbin");
				 */

				if (deleted_orbiting > 0)
					Debug.Log("Removed " + deleted_orbiting + " debris orbiting in-atmosphere");

				if (deleted_landed > 0)
					Debug.Log("Removed " + deleted_landed + " debris landed at Kerbin");
			}

		}

		public void simulateDebrisDrag()
		{

			if (FlightGlobals.ready && HighLogic.LoadedSceneIsFlight && TimeWarp.deltaTime > 0)
			{
				foreach (Vessel vessel in FlightGlobals.Vessels)
				{
					if (!vessel.loaded && !vessel.isCommandable && !vessel.LandedOrSplashed
						&& vessel.mainBody.atmosphere)
					{
						
						//if (vessel.altitude < vessel.mainBody.maxAtmosphereAltitude)
						{

							/*
							double speed = vessel.GetSrfVelocity().magnitude;

							double adjusted_speed = speed / REF_SPEED;
							double speed_factor = adjusted_speed * adjusted_speed;

							double drag_deccel = atmosphere_density * DRAG_FACTOR * speed_factor * (TimeWarp.CurrentRate * TIMESTEP);
							 */

							//if (drag_deccel > 0.0)
							{
								double ut = Planetarium.GetUniversalTime();

								//double speed_mult = (speed - drag_deccel)/speed;
								double speed_mult = 1.0;

								Vector3d before = vessel.orbit.getOrbitalVelocityAtUT(ut);
								Vector3d pos_before = vessel.orbit.getPositionAtUT(ut);

								Vector3d orbit_vel = vessel.orbit.getOrbitalVelocityAtUT(ut);

								//For some reason you have to offset ut by a timestep or the resulting orbit is wrong
								vessel.orbit.UpdateFromStateVectors(
									vessel.orbit.getRelativePositionAtUT(ut),
									orbit_vel * speed_mult,
									vessel.orbit.referenceBody,
									ut
									);

								Vector3d after = vessel.orbit.getOrbitalVelocityAtUT(ut);
								Vector3d pos_after = vessel.orbit.getPositionAtUT(ut);

								Debug.Log(" Vdiff: " + Vector3d.Distance(before, after).ToString()
									+ " Pdiff: " + Vector3d.Distance(pos_before, pos_after).ToString());
							}
						}

					}
				}

			}
		}

		public void displayDebugInfo()
		{
			List<Guid> remoused_vessel = new List<Guid>();

			if (FlightGlobals.ready && HighLogic.LoadedSceneIsFlight) {
				foreach (Vessel vessel in FlightGlobals.Vessels) {

					if (isValidForLanded(vessel))
					{
						double ksc_distance_factor = getDistanceFactorFromKSC(vessel);
						double ksc_distance = getDistanceFromKSC(vessel);
						double time_left = getLandedCleanupTime(vessel) - vessel.missionTime;

						Debug.Log(
							vessel.vesselName + " Dist Factor: " + ksc_distance_factor.ToString("0.00")
							+ " KSC Dist: " + ksc_distance.ToString("0.00")
							+ " Time left: " + (time_left / 3600.0).ToString("0.00")
							);

						Debug.Log(
							"Lat: " + vessel.latitude + " Lon: " + vessel.longitude
							);

					}
					else if (isValidForDecay(vessel)) {
	
						double total_ats = getTotalAtmosphereSeconds(vessel);
						double ats_per_orbit = getAtmosphereSecondsPerOrbit(vessel.orbit);
						double time_left = (ORBIT_CLEANUP_ATMOS_SECS - total_ats)/(ats_per_orbit / vessel.orbit.period);

						Debug.Log(
							vessel.vesselName + " Total ATs: " + total_ats
							+ " ATs per orbit: " + ats_per_orbit
							+" Time left: " + (time_left / 3600.0).ToString("0.00")
							);
					}

				}
			}
		}

		//MonoBehaviour

		public void Awake()
		{
			DontDestroyOnLoad(this);
			CancelInvoke();
			//InvokeRepeating("simulateDebrisDrag", 0.0f, TIMESTEP);
			InvokeRepeating("cleanUpDebris", 0.0f, CLEANUP_INTERVAL);
		}

		public void Update()
		{
			if (Input.GetKeyDown(KeyCode.LeftControl))
				controlDown = true;

			if (Input.GetKeyUp(KeyCode.LeftControl))
				controlDown = false;

			if (controlDown && Input.GetKeyDown(KeyCode.P) && FlightGlobals.ready)
				cleanUpDebris();

			if (controlDown && Input.GetKeyDown(KeyCode.O) && FlightGlobals.ready)
				displayDebugInfo();

			if (!kscRadialVectorFound && FlightGlobals.ready)
			{
				foreach (CelestialBody body in FlightGlobals.Bodies)
				{
					if (body.bodyName == "Kerbin")
					{
						Vector3d ksc_pos = body.GetWorldSurfacePosition(-0.102668048653556, -74.5753856554463, 0.0);
						kscRadialVector = ksc_pos - body.position;
						kscRadialVector.Normalize();
						kscRadialVectorFound = true;
						break;
					}
				}
			}
		}

	}
}
