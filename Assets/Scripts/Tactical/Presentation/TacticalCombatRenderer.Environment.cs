using System;
using UnityEngine;
using ShadowCastingMode = global::UnityEngine.Rendering.ShadowCastingMode;
using TacticalCombatEnvironmentDefinition = global::Rebellion.Game.SpaceCombat.TacticalCombatEnvironmentDefinition;
using TacticalCombatFighterSprites = global::Rebellion.Game.SpaceCombat.TacticalCombatFighterSprites;
using TacticalCombatTextureOverride = global::Rebellion.Game.SpaceCombat.TacticalCombatTextureOverride;

namespace Rebellion.Tactical.Presentation
{
    public sealed partial class TacticalCombatRenderer
        : MonoBehaviour,
            ITacticalCombatArrivalPresentation
    {
        /// <summary>
        /// Loads the selected planet's authored scenery and palette-dependent texture substitutions.
        /// </summary>
        /// <param name="session">The captured encounter environment.</param>
        /// <param name="assets">The addressed content source.</param>
        /// <param name="showPlanet">Whether the planet image and its scenery-position draws are enabled.</param>
        private void PrepareEnvironment(
            TacticalCombatSession session,
            ContentAssets assets,
            bool showPlanet
        )
        {
            TacticalCombatEnvironmentDefinition environment = session.Environment;
            if (environment == null)
                return;
            foreach (TacticalCombatTextureOverride replacement in environment.TextureOverrides)
                _environmentTextures.Add(
                    replacement.TextureName,
                    assets.GetTexture(replacement.ImagePath)
                        ?? throw new InvalidOperationException(
                            "A tactical environment texture is missing: " + replacement.ImagePath
                        )
                );
            if (!showPlanet || string.IsNullOrWhiteSpace(environment.PlanetImagePath))
                return;
            Texture2D texture =
                assets.GetTexture(environment.PlanetImagePath)
                ?? throw new InvalidOperationException(
                    "The tactical planet image is missing: " + environment.PlanetImagePath
                );
            GameObject planet = CreateBillboard();
            planet.name = "Planet";
            planet.transform.SetParent(_modelRoot, false);
            planet.transform.localPosition = GetSceneryPosition(session.BattleDepth, 2, 0.1f);
            planet.transform.localScale = Vector3.one * session.BattleDepth;
            Material material = new Material(_fighterShader) { mainTexture = texture };
            _fighterMaterials.Add(texture, material);
            planet.GetComponent<MeshRenderer>().sharedMaterial = material;
            SetLayer(planet.transform, PresentationLayers.TacticalCombat);
            _planet = planet.transform;
        }

        /// <summary>
        /// Creates renderer-owned material variants while preserving the shared model cache's materials.
        /// </summary>
        /// <param name="model">The instantiated model hierarchy.</param>
        private void ApplyEnvironmentTextures(Transform model)
        {
            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material original = materials[index];
                    if (
                        original == null
                        || original.mainTexture == null
                        || !_environmentTextures.TryGetValue(
                            original.mainTexture.name,
                            out Texture2D texture
                        )
                    )
                        continue;
                    if (!_environmentMaterials.TryGetValue(original, out Material replacement))
                    {
                        replacement = new Material(original) { mainTexture = texture };
                        _environmentMaterials.Add(original, replacement);
                    }
                    materials[index] = replacement;
                    changed = true;
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// Places background scenery using the encounter-owned sequence and shared battle seed boundary.
        /// </summary>
        /// <param name="depth">The battle's deployment span.</param>
        /// <param name="horizontal">The horizontal position in multiples of the span.</param>
        /// <param name="spread">The vertical and depth spread in multiples of the span.</param>
        /// <returns>The encounter-local scenery position.</returns>
        private Vector3 GetSceneryPosition(float depth, float horizontal, float spread) =>
            ToUnity(_sceneryRandom.NextPosition(depth, horizontal, spread));

        /// <summary>
        /// Converts the viewport's near-plane half extent into the camera's vertical angle.
        /// </summary>
        private void UpdateFieldOfView()
        {
            if (_detailProjection?.Field.Equals(_field) != true)
                _detailProjection = new TacticalCombatDetailProjection(_field);
            float vertical = _field / Mathf.Max(_battleCamera.aspect, 1);
            _battleCamera.fieldOfView = 2 * Mathf.Atan(vertical) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Creates one billboard with the pack-authored fighter sprite.
        /// </summary>
        /// <param name="visual">The fighter's owned visual.</param>
        /// <param name="assets">The addressed texture source.</param>
        /// <param name="session">The encounter used to select friendly group colors.</param>
        private void CreateFighter(
            Visual visual,
            ContentAssets assets,
            TacticalCombatSession session
        )
        {
            TacticalCombatFighterSprites sprites =
                visual.Unit.Definition?.FighterSprites
                ?? throw new InvalidOperationException(
                    $"Fighter sprites are missing for '{visual.Unit.TypeID}'."
                );
            int color = 4;
            if (visual.Unit.Side == session.PlayerSide)
            {
                TacticalCombatGroup requested = session.GetRequestedGroup(visual.Unit);
                color = requested?.IsFighterGroup == true ? requested.FighterCategory : 0;
            }
            visual.FighterTextures = new Texture2D[3];
            visual.FighterTextures[0] = assets.GetTexture(sprites.NearImages[color]);
            visual.FighterTextures[1] = assets.GetTexture(sprites.MiddleImages[color]);
            visual.FighterTextures[2] = assets.GetTexture(sprites.FarImages[color]);
            foreach (Texture2D texture in visual.FighterTextures)
                if (texture == null)
                    throw new InvalidOperationException(
                        $"A fighter sprite is missing for '{visual.Unit.TypeID}'."
                    );
            GameObject billboard = CreateBillboard();
            billboard.name = "Fighter";
            billboard.transform.SetParent(visual.Root.transform, false);
            billboard.transform.localScale = Vector3.one * 0.3f;
            visual.Billboard = billboard.transform;
            visual.FighterRenderer = billboard.GetComponent<MeshRenderer>();
            visual.FighterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            visual.FighterRenderer.receiveShadows = false;
        }

        /// <summary>
        /// Creates the station's complete or construction billboard using its shared tactical definition.
        /// </summary>
        /// <param name="visual">The owned station visual.</param>
        /// <param name="assets">The addressed texture source.</param>
        /// <param name="battleDepth">The encounter's deployment span.</param>
        private void CreateStation(Visual visual, ContentAssets assets, float battleDepth)
        {
            string path =
                visual.Unit.Status == TacticalCombatUnitStatus.UnderConstruction
                    ? visual.Unit.Definition.ConstructionBillboardImagePath
                    : visual.Unit.Definition.BillboardImagePath;
            Texture2D texture =
                assets.GetTexture(path)
                ?? throw new InvalidOperationException(
                    $"The station image is missing for '{visual.Unit.TypeID}'."
                );
            Material material = new Material(_fighterShader) { mainTexture = texture };
            _fighterMaterials.Add(texture, material);
            GameObject billboard = CreateBillboard();
            billboard.transform.SetParent(visual.Root.transform, false);
            billboard.transform.localScale = Vector3.one * (battleDepth * 0.5f);
            billboard.GetComponent<MeshRenderer>().sharedMaterial = material;
            visual.Billboard = billboard.transform;
            visual.StationPosition = GetSceneryPosition(battleDepth, -2, 0.2f);
        }

        /// <summary>
        /// Creates a render-only billboard from the screen's shared quad geometry.
        /// </summary>
        /// <returns>A billboard with a mesh filter and renderer.</returns>
        private GameObject CreateBillboard()
        {
            if (_billboardMesh == null)
            {
                _billboardMesh = new Mesh
                {
                    name = "Tactical billboard",
                    vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0),
                        new Vector3(0.5f, -0.5f, 0),
                        new Vector3(-0.5f, 0.5f, 0),
                        new Vector3(0.5f, 0.5f, 0),
                    },
                    uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                    triangles = new[] { 0, 2, 1, 2, 3, 1 },
                };
                _billboardMesh.RecalculateBounds();
            }
            GameObject billboard = new GameObject(
                "Billboard",
                typeof(MeshFilter),
                typeof(MeshRenderer)
            );
            billboard.GetComponent<MeshFilter>().sharedMesh = _billboardMesh;
            return billboard;
        }
    }
}
