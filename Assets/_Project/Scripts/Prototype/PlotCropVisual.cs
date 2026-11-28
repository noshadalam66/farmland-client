using UnityEngine;

namespace FarmLand.Empire.Prototype
{
    public sealed class PlotCropVisual : MonoBehaviour
    {
        [SerializeField] private GameObject wheat;
        [SerializeField] private GameObject corn;
        [SerializeField] private GameObject rice;
        [SerializeField] private GameObject readyIndicator;

        private GameObject displayedModel;

        public void Configure(GameObject wheatModel, GameObject cornModel, GameObject riceModel, GameObject indicator)
        {
            wheat = wheatModel;
            corn = cornModel;
            rice = riceModel;
            readyIndicator = indicator;
        }

        public void Show(string crop, float growth)
        {
            GameObject model = crop switch
            {
                "corn" => corn,
                "rice" => rice,
                _ => wheat,
            };
            if (model != displayedModel)
            {
                wheat.SetActive(model == wheat);
                corn.SetActive(model == corn);
                rice.SetActive(model == rice);
                displayedModel = model;
            }
            model.transform.localScale = new Vector3(1, Mathf.Lerp(0.3f, 1, growth), 1);
            bool ready = growth >= 1;
            if (readyIndicator.activeSelf != ready) readyIndicator.SetActive(ready);
        }
    }
}
