# Stage Stutter Fix
When you activate the Teleporter or a portal to leave the stage, the game immediately begins preloading assets for the next stage. For some people this causes a small lag spike. This mod fixes* that stutter by spreading the preloading of assets across multiple frames.

*Please note that some minor stutters may still occur on frames where expensive assets are being preloaded. To completely fix all stuttering, you can disable the preloading of stage assets entirely (see below).

## Config
This mod has one config setting, `Max preload time per frame`, which controls maximum time per frame (in milliseconds) to spend preloading the next stage. The lower this value is, the less stuttering you will experience. The default value of 1ms should be fine for most people. A value of zero or less will disable stage preloading entirely, which will prevent any stuttering but may increase stage load times.

## Contact
You can find me in the [RoR2 Modding Server](https://discord.gg/5MbXZvd) @groove_salad

Or, you can post issues and feedback on the [GitHub](https://github.com/Priscillalala/StageStutterFix/issues)

## Donations
If this mod was helpful, consider [buying me a coffee](https://www.buymeacoffee.com/groovesalad)!

<a href="https://www.buymeacoffee.com/groovesalad" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" height=60 width=217></a>