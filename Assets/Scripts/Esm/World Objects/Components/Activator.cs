using UnityEngine;

namespace Esm
{
	[SelectionBase]
	public class Activator : RecordBehaviour<Activator, ActivatorRecord>, IActivatable
	{
		private InfoPanel infoPanel;

		void IActivatable.Activate(GameObject target)
		{

		}

		void IActivatable.DisplayInfo()
		{
			infoPanel = record.DisplayInfo(transform.position, referenceData);
		}

		void IActivatable.CloseInfo()
		{
			if (infoPanel == null)
			{
				return;
			}

			Destroy(infoPanel.gameObject);
			infoPanel = null;
		}
	}
}