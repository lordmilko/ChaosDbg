# Splitter

ChaosDbg contains a split pane system similar to that of Visual Studio. This document describes the design of the splitter system and how its various components interact.

## Overview

There are several controls that the splitter system is comprised of

* DockContainer
    * SplitterItemsDockContainer
    * TabControlDockContainer
* SplitterItemsControl
* TabControlEx
* SplitterPanel
* SplitterItem
* SplitterGrip

At the root of the tree is a `SplitterItemsDockContainer`. When used at the top of the tree, `SplitterItemsDockContainer` is simply a lightweight control that encapsulates a `SplitterItemsControl`.
`SplitterItemsDockContainer` contains additional properties over `SplitterItemsControl` that can be used to manage the size of the control, however these properties do not apply with the root-most element.

`SplitterItemsControl` is a custom implementation of an `ItemsControl`. Controls that were defined as the logical children of the `SplitterItemsDockContainer` get wrapped in a `SplitterItem` for the purposes of being rendered in the visual tree, where they are rendered inside of a `SplitterPanel` control (Additional information on the design of `ItemsControl` types can be found [here](http://drwpf.com/blog/itemscontrol-a-to-z/) (in particular, articles "I" and "G"))

Thus, the following declaration

```xml
<SplitterItemsDockContainer>
    <TabControlDockContainer />
</SplitterItemsDockContainer>
```

expands to

```xml
<SplitterItemsDockContainer>
    <SplitterItemsControl>
        <SplitterPanel>
            <SplitterItem>
                <TabControlDockContainer />
            </SplitterItem>
        </SplitterPanel>
    </SplitterItemsControl>
</SplitterItemsDockContainer>
```

## DockContainer

All controls that are declared within a `SplitterItem` must derive from `DockContainer`. The `DockContainer` type contains additional properties on it that can be used to control the size of a "normal control" when mounted inside of a `SplitterItemsControl`. Sizing properties defined on the `DockContainer` controls are relayed to the parent `SplitterPanel` via the intermediate `SplitterItem` control. Currently, there are two types of `DockContainer` that exist

* `SplitterItemsDockContainer` - encapsulates a `SplitterItemsControl`. Can be mounted inside a parent `SplitterItemsDockContainer` to further subdivide the layout
* `TabControlDockContainer` - encapsulates a `TabControlEx`, which is a normal `TabControl` with additional enhancements specific to ChaosDbg

The layout of the children within a given `SplitterItemsDockContainer` is determined by the `Orientation` property of the container. The `Orientation` property describes how the *panes* should be laid out in relation to one another. Thus, an orientation of type `Horizontal` would result in two panes side by side, with a vertical splitter between them, while `Vertical` would result in panes stacked above each other in a single column with horizontal splitters between them

## Sizing

`DockContainer` controls can be sized within their parents via the `DockedWidth` and `DockedHeight` dependency properties. The pane system supports two sizing modes: proportional and fill.

In proportional mode, controls signify how much space they would like to occupy within their parent relative to their siblings. By default, all controls request 100% of the available space. Thus, in the following example

```xml
<SplitterItemsDockContainer>
    <SplitterItemsDockContainer />
    <SplitterItemsDockContainer />
</SplitterItemsDockContainer>
```
We have two child controls, both of which want 100% of the available space. We obviously can't give away 200% of the space,
but when we consider the *proportion* of this space that each control requested, we can say that each control is *entitled* to 50% of the available space. However, if we instead consider the following

```xml
<SplitterItemsDockContainer>
    <SplitterItemsDockContainer DockedWidth="200" />
    <SplitterItemsDockContainer />
</SplitterItemsDockContainer>
```
In total the siblings are asking for 300% of the available space, the effect of which will be the first sibling will get 66% of the available space, while the second sibling will only get the remaining 33%. Thus, when only a single sibling requests for additional space, it can easily be seen that 200% will give you twice as much space, 300% will give 3x as much space, etc.

In certain circumstances, it may be desirable to give a certain control as much space as possible, after processing proportionally based controls. In these circumstances, fill sizing can be used

```xml
<SplitterItemsDockContainer>
    <SplitterItemsDockContainer DockedWidth="*" />
    <SplitterItemsDockContainer />
</SplitterItemsDockContainer>
```

When a pane indicates it wishes to fill the remaining available space, the `DockedWidth` and `DockedHeight` are interpreted as literal pixel values. Thus, in the above example, the second sibling would be 100px wide and the first sibling would fill the remaining available space.

When multiple siblings indicate they wish to perform a fill, the amount of space remaining for filling is equally divided between them. Thus in the following example

<SplitterItemsDockContainer>
    <SplitterItemsDockContainer DockedWidth="*" />
    <SplitterItemsDockContainer DockedWidth="*" />
    <SplitterItemsDockContainer />
</SplitterItemsDockContainer>

the third sibling would be 100px wide and the first two siblings would equally split the remaining available space.

When multiple siblings specify a proportional value alongside a sibling that wishes to fill, all siblings' lengths would be honored. Thus in the following example

<SplitterItemsDockContainer>
    <SplitterItemsDockContainer DockedWidth="*" />
    <SplitterItemsDockContainer />
    <SplitterItemsDockContainer />
</SplitterItemsDockContainer>

the second and third siblings would be 100px wide and the first sibling would fill the remaining available space.