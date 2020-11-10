using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Tiles.CustomRules
{
    [CreateAssetMenu(menuName = "2D/Tiles/Sibling Tile")]
    public class SiblingTile : RuleTile<SiblingTile.Neighbor>
    {
        public List<TileBase> Siblings = new List<TileBase>();

        public override bool RuleMatch(int neighbor, TileBase tile)
        {
            switch (neighbor)
            {
                case TilingRuleOutput.Neighbor.This:
                    // Direct override of rule tile's "this" check with an inclusion of those in Siblings list.
                    return tile == this || Siblings.Contains(tile);
                case Neighbor.SiblingOnly:
                    // Sibling tile check only.
                    return Siblings.Contains(tile);
                case Neighbor.ThisOnly:
                    // This tile rule only. Used to be "this".
                    return tile == this;
            }

            return base.RuleMatch(neighbor, tile);
        }

        public class Neighbor : TilingRuleOutput.Neighbor
        {
            public const int SiblingOnly = 3;
            public const int ThisOnly = 4;
        }
    }
}