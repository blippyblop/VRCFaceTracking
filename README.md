# What is this fork?

I forked the main line code in order to debug the new native VRCFT stuff. Turns out the main code does actually send the right data, but for some reason it didn't when I tried? or something? 

Stuff added:

- VRC Native FT Param debug
- An actual indicator if you're sending native FT data or the usual (Jerry's compatible) data
- Jerry's style processing for native FT.

## Jerry's style?

Yeah- The prefab does a load of processing to the raw OSC data before turning it into blendshape data. VRCFT native does.. no processing I think. OSC info is sent right into blendshapes. 

This fork takes all of what jerry's prefab does and modifies the osc data before sending it to VRC, where previously it was modified in VRC using the animator controller logic. Yeah.

Does it work? This is still in testing. My experience with native FT so far has been pretty bad but this might be me ruining it up more than anything else.


# 👀 VRCFaceTracking

Provides eye tracking and lip tracking in VRChat by providing a bridge between your tracking hardware and VRChat's OSC server.
## [Get started here!](https://docs.vrcft.io/docs/intro/getting-started)

[![Discord](https://discord.com/api/guilds/849300336128032789/widget.png)](https://discord.com/invite/vrcft)

## 🎥 Demo

[![](https://i.imgur.com/iQkw12C.jpg)](https://youtu.be/ZTVnh8aaf9U)

## 🛠 Avatar Setup

For this app to work, you'll need to be using an avatar with the correct parameters or an avatar config file with the correct mappings. The system is designed to control your avatar's eyes and lips via simple blend states but what the parameters control is completely up to you.

### [List of Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/)

## 👀 [Eye Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/#eye-tracking-parameters)

### [Eye Tracking Setup Guide](https://github.com/benaclejames/VRCFaceTracking/wiki/Eye-Tracking-Setup)

It's not required to use all of these parameters. In fact, you don't need to use any of them if you intend on using VRChat's built-in eye tracking system. Similar to the setup of parameters with Unity Animation Controllers, these are all case-sensitive and must be copied **EXACTLY** as shown into your Avatar's base parameters. A typical setup might look something like this:<br>
![](https://i.imgur.com/kfJD1Bl.png)

We strongly encourage you to [consult the docs](https://docs.vrcft.io) for a setup guide and more info as to what each parameter does

## :lips: [Lip and Face Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/#expression-tracking-parameters)

There are a large number of parameters you can use for lip and face tracking. 

### [Combined Lip Parameters](https://docs.vrcft.io/docs/tutorial-avatars/tutorial-avatars-extras/parameters/#addtional-simplified-tracking-parameters) - Combined parameters to group mutually exclusive face shapes.

## ⛓ External Modules

Use the module registry to download addons and add support for your hardware!
