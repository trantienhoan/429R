These two materials only exist so web builds include the shaders the game creates materials from in code
(Gfx.cs: Shader.Find "Universal Render Pipeline/Lit" and ".../Particles/Unlit"). Without them, the sparks,
puffs and smoke could turn pink in a build. Don't delete them.
Keep_URP_Lit has _EMISSION on because Gfx.NewLit enables it on every material it makes; the plain variant comes in
through the imported models' own materials.
