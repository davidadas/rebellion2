using System;
using Rebellion.Game.Combat;
using Rebellion.UI;
using UnityEngine;
using ShadowCastingMode = global::UnityEngine.Rendering.ShadowCastingMode;
using TacticalCombatEnvironmentDefinition = global::Rebellion.Content.TacticalCombatEnvironmentDefinition;
using TacticalCombatFighterSprites = global::Rebellion.Content.TacticalCombatFighterSprites;
using TacticalCombatTextureOverride = global::Rebellion.Content.TacticalCombatTextureOverride;
using TacticalCombatUnitDefinition = global::Rebellion.Content.TacticalCombatUnitDefinition;

namespace Rebellion.Tactical.Rendering
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
            TacticalCombatFighterSprites sprites = GetUnitDefinition(visual.Unit)?.FighterSprites;
            if (sprites == null)
            {
                CreatePlaceholderBillboard(visual, 0.3f);
                return;
            }
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
            TacticalCombatUnitDefinition definition = GetUnitDefinition(visual.Unit);
            string path =
                visual.Unit.Status == CombatUnitStatus.UnderConstruction
                    ? definition?.ConstructionBillboardImagePath
                    : definition?.BillboardImagePath;
            visual.StationPosition = GetSceneryPosition(battleDepth, -2, 0.2f);
            if (string.IsNullOrWhiteSpace(path))
            {
                CreatePlaceholderBillboard(visual, battleDepth * 0.1f);
                return;
            }
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
        }

        /// <summary>
        /// Uses ship geometry for an on-screen contact when no capital-ship model is authored.
        /// </summary>
        /// <param name="visual">The owned ship visual.</param>
        private void CreateCapitalPlaceholder(Visual visual)
        {
            if (_contactMesh == null)
            {
                _contactMesh = new Mesh
                {
                    name = "Ship contact",
                    vertices = new[]
                    {
                        0.5f * Vector3.forward,
                        0.5f * Vector3.right,
                        0.5f * Vector3.back,
                        0.5f * Vector3.left,
                        0.5f * Vector3.up,
                        0.5f * Vector3.down,
                    },
                    triangles = new[]
                    {
                        4,
                        1,
                        0,
                        4,
                        2,
                        1,
                        4,
                        3,
                        2,
                        4,
                        0,
                        3,
                        5,
                        0,
                        1,
                        5,
                        1,
                        2,
                        5,
                        2,
                        3,
                        5,
                        3,
                        0,
                    },
                };
                _contactMesh.RecalculateBounds();
            }

            GameObject marker = new("Ship contact", typeof(MeshFilter), typeof(MeshRenderer));
            marker.GetComponent<MeshFilter>().sharedMesh = _contactMesh;
            marker.transform.SetParent(visual.Root.transform, false);
            Vector3 dimensions = ToUnity(visual.Unit.HullDimensions);
            if (dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0)
                dimensions = new Vector3(
                    visual.Unit.CollisionRadius,
                    visual.Unit.HalfHeight * 2,
                    visual.Unit.CollisionRadius * 2
                );
            marker.transform.localScale = dimensions;
            marker.GetComponent<MeshRenderer>().sharedMaterial = GetPlaceholderMaterial();
            visual.ModelDetails = new[] { marker.transform };
        }

        /// <summary>
        /// Presents a visible contact for a fighter or station with no authored artwork.
        /// </summary>
        /// <param name="visual">The contact's owned visual.</param>
        /// <param name="scale">The billboard's width in scene units.</param>
        private void CreatePlaceholderBillboard(Visual visual, float scale)
        {
            GameObject billboard = CreateBillboard();
            billboard.name = "Contact";
            billboard.transform.SetParent(visual.Root.transform, false);
            billboard.transform.localScale = Vector3.one * scale;
            billboard.GetComponent<MeshRenderer>().sharedMaterial = GetPlaceholderMaterial();
            visual.Billboard = billboard.transform;
        }

        /// <summary>
        /// Reuses one unlit material for contacts without artwork.
        /// </summary>
        /// <returns>The screen-owned placeholder material.</returns>
        private Material GetPlaceholderMaterial()
        {
            Texture2D texture = Texture2D.whiteTexture;
            if (!_fighterMaterials.TryGetValue(texture, out Material material))
            {
                material = new Material(_fighterShader) { mainTexture = texture };
                _fighterMaterials.Add(texture, material);
            }
            return material;
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
