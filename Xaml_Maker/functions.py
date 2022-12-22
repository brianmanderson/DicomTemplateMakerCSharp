#!/usr/bin/env python
# coding: utf-8

# In[ ]:


# Import modules with shorthand
import numpy as np
import pandas as pd
import re
import xml.etree.ElementTree as ET


# In[ ]:


def convert_top(idx,data,tree,root,filename):

    Item = ET.SubElement(root.find('.//Prescription'),'Item')
    Type = ET.SubElement(Item,'Type')
    Modifier = ET.SubElement(Item,'Modifier')
    Parameter = ET.SubElement(Item,'Parameter')
    Dose = ET.SubElement(Item,'Dose')
    TotalDose = ET.SubElement(Item,'TotalDose')


    #this is how you would change the text for an element without attributes
    root.find('.//Prescription//Item['+str(idx)+']//Type').text = str(data[1])   
    root.find('.//Prescription//Item['+str(idx)+']//Modifier').text = str(data[2])   
    root.find('.//Prescription//Item['+str(idx)+']//Parameter').text = str(data[3])   
    root.find('.//Prescription//Item['+str(idx)+']//Dose').text = str(data[4])   
    root.find('.//Prescription//Item['+str(idx)+']//TotalDose').text = str(data[5])  

    
    #this is how you would change the item ID (or any element's attribute)
    root.find('.//Prescription//Item['+str(idx)+']').set('ID', data[0])
    root.find('.//Prescription//Item['+str(idx)+']').set('Primary', 'false')



    tree.write(filename)
    
def convert_bottomDV(idx,data,tree,root,filename):
    Item = ET.SubElement(root.find('.//Prescription'),'MeasureItem')
    Type = ET.SubElement(Item,'Type')
    Modifier = ET.SubElement(Item,'Modifier')
    Value = ET.SubElement(Item,'Value')
    TypeSpecifier = ET.SubElement(Item,'TypeSpecifier')
    ReportDQPValueInAbsoluteUnits = ET.SubElement(Item,'ReportDQPValueInAbsoluteUnits')
    
    #this is how you would change the item ID (or any element's attribute)
    root.find('.//Prescription//MeasureItem['+str(idx) + ']').set('ID', data[0])
    
    #this is how you would change the text for an element without attributes
    root.find('.//Prescription//MeasureItem['+str(idx)+ ']//Type').text = str(data[1])   
    root.find('.//Prescription//MeasureItem['+str(idx)+ ']//Modifier').text = str(data[2])   
    root.find('.//Prescription//MeasureItem['+str(idx)+ ']//Value').text = str(data[3])   
    root.find('.//Prescription//MeasureItem['+str(idx)+ ']//TypeSpecifier').text = str(data[4])   
    root.find('.//Prescription//MeasureItem['+str(idx)+ ']//ReportDQPValueInAbsoluteUnits').text = data[5]   
    
    tree.write(filename)  

def convert_bottomI(idx,data,tree,root,filename):
    Item = ET.SubElement(root.find('.//Prescription'),'MeasureItem')
    Type = ET.SubElement(Item,'Type')
    Modifier = ET.SubElement(Item,'Modifier')
    Value = ET.SubElement(Item,'Value')
    TypeSpecifier = ET.SubElement(Item,'TypeSpecifier')
    ReportDQPValueInAbsoluteUnits = ET.SubElement(Item,'ReportDQPValueInAbsoluteUnits')
    
    #this is how you would change the item ID (or any element's attribute)
    root.find('.//Prescription//MeasureItem['+str(idx) + ']').set('ID', data[0])
    
    #this is how you would change the text for an element without attributes
    root.find('.//Prescription//MeasureItem['+str(idx)+ ']//Type').text = str(data[1])   
    root.find('.//Prescription//MeasureItem['+str(idx)+ ']//Modifier').text = str(data[2])   
    root.find('.//Prescription//MeasureItem['+str(idx)+ ']//Value').text = str(data[3])   
    root.find('.//Prescription//MeasureItem['+str(idx) + ']//TypeSpecifier').set('xsi:nil', 'true')
    root.find('.//Prescription//MeasureItem['+str(idx)+ ']//ReportDQPValueInAbsoluteUnits').text = data[5]   
    
    tree.write(filename)   
    
def structure(idx,data,tree,root,filename):
    
    #NOTE THAT THE DATA SET HERE SHOULD BE FOR STRUCTURE INFO NOT DOSE CONSTRAINTS.
    #data: Structure ID, volume type, type index, Color and style, Search CT low, search CT high, DVH line style DVh line color
    #DVH line width, EUDAlpha, TCPAlpha, TCPBeta,TCPGamma

    """
    Note that these have to be in order... a bit ridiculous
    """
    Structure = ET.SubElement(root.find('.//Structures'), 'Structure')
    Structure.set("ID", data.StrID)
    Structure.set("Name", data.StrID)
    Identification = ET.SubElement(Structure,'Identification')
    VolumeID = ET.SubElement(Identification,'VolumeID')
    VolumeCode = ET.SubElement(Identification,'VolumeCode')
    VolumeType = ET.SubElement(Identification,'VolumeType')
    VolumeType.text = data.Type
    VolumeCodeTable = ET.SubElement(Identification,'VolumeCodeTable')
    StructureCode = ET.SubElement(Identification,'StructureCode')
    StructureCode.set('Code', data.iCode)
    StructureCode.set('CodeScheme', data.iCodeScheme)
    StructureCode.set('CodeSchemeVersion', "3.2")
    TypeIndex = ET.SubElement(Structure,'TypeIndex')
    TypeIndex.text = str(data.TypeIdx)
    ColorAndStyle = ET.SubElement(Structure,'ColorAndStyle')
    ColorAndStyle.text = data.Color
    SearchCTLow = ET.SubElement(Structure,'SearchCTLow')
    SearchCTLow.set('xsi:nil', 'true')
    SearchCTHigh = ET.SubElement(Structure,'SearchCTHigh')
    SearchCTHigh.set('xsi:nil', 'true')
    DVHLineStyle = ET.SubElement(Structure,'DVHLineStyle')
    DVHLineStyle.text = str(data.DVHLS)
    DVHLineColor = ET.SubElement(Structure,'DVHLineColor')
    DVHLineColor.text = str(data.DVHLC)
    DVHLineWidth = ET.SubElement(Structure,'DVHLineWidth')
    DVHLineWidth.text = str(data.DVHLW)
    EUDAlpha = ET.SubElement(Structure,'EUDAlpha')
    EUDAlpha.set('xsi:nil', 'true')
    TCPAlpha = ET.SubElement(Structure,'TCPAlpha')
    TCPAlpha.set('xsi:nil', 'true')
    TCPBeta = ET.SubElement(Structure,'TCPBeta')
    TCPBeta.set('xsi:nil', 'true')
    TCPGamma = ET.SubElement(Structure,'TCPGamma')
    TCPGamma.set('xsi:nil', 'true')

    
    tree.write(filename)

