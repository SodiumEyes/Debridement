using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Debridement
{
	class DebrisSalvageModule : PartModule
	{

		public const double MAX_SALVAGE_DIST = 50;

		public override void OnAwake()
		{
			if (DebridementManager.GameObjectInstance == null)
			{
				Debug.Log("*** Debridement started");
				DebridementManager.GameObjectInstance = GameObject.Find("DebridementManager") ?? new GameObject("DebridementManager", typeof(DebridementManager));
			}
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			Events["salvageDebris"].guiActiveUnfocused = vessel.LandedOrSplashed;
		}

		[KSPEvent(guiName = "Salvage Nearby Debris", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = false, unfocusedRange = 10f)]
		public void salvageDebris()
		{
			Queue<Vessel> delete_queue = new Queue<Vessel>();

			foreach (Vessel debris in FlightGlobals.Vessels)
			{
				if (debris != vessel && debris.loaded && !debris.isCommandable && debris.mainBody == vessel.mainBody
					&& Vector3d.Distance(debris.GetWorldPos3D(), vessel.GetWorldPos3D()) <= MAX_SALVAGE_DIST)
				{
					//Add the vessel to the delete queue
					delete_queue.Enqueue(debris);
				}

			}

			//Delete debris
			while (delete_queue.Count > 0)
			{
				Vessel debris = delete_queue.Dequeue();

				//Recover resources in deleted debris
				foreach (Part p in debris.Parts)
				{
					foreach (PartResource resource in p.Resources) {
						if (resource.amount > 0.0)
						{
							Debug.Log("Salvaged resource: " + resource.resourceName + " amount: " + resource.amount);
							double received = part.RequestResource(resource.resourceName, -resource.amount);
							resource.amount += received;
						}
					}
				}

				debris.Die();
			}
		}

	}
}
