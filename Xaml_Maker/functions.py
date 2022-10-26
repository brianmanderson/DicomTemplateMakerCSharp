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

    Structure = ET.SubElement(root.find('.//Structures'),'Structure')
    Identification = ET.SubElement(Structure,'Identification')
    VolumeID = ET.SubElement(Identification,'VolumeID')
    VolumeCode = ET.SubElement(Identification,'VolumeCode')
    VolumeType = ET.SubElement(Identification,'VolumeType')
    VolumeCodeTable = ET.SubElement(Identification,'VolumeCodeTable')
    TypeIndex = ET.SubElement(Structure,'TypeIndex')
    ColorAndStyle = ET.SubElement(Structure,'ColorAndStyle')
    SearchCTLow = ET.SubElement(Structure,'SearchCTLow')
    SearchCTHigh = ET.SubElement(Structure,'SearchCTHigh')
    DVHLineStyle = ET.SubElement(Structure,'DVHLineStyle')
    DVHLineColor = ET.SubElement(Structure,'DVHLineColor')
    DVHLineWidth = ET.SubElement(Structure,'DVHLineWidth')
    EUDAlpha = ET.SubElement(Structure,'EUDAlpha')
    TCPAlpha = ET.SubElement(Structure,'TCPAlpha')
    TCPBeta = ET.SubElement(Structure,'TCPBeta')
    TCPGamma = ET.SubElement(Structure,'TCPGamma')

    #this is how you would change the item ID (or any element's attribute)
    root.find('.//Structures//Structure['+str(idx)+']').set('ID', data[0])
    root.find('.//Structures//Structure['+str(idx)+']').set('Name', '')
    
    #Set Identification sub-element values
#     root.find('.//Structures//Structure['+str(idx)+']//Identification//VolumeID').text = str(data[1])   
#     root.find('.//Structures//Structure['+str(idx)+']//Identification//VolumeCode').text = str(data[2])   
    root.find('.//Structures//Structure['+str(idx)+']//Identification//VolumeType').text = str(data[1])      
#     root.find('.//Structures//Structure['+str(idx)+']//Identification//VolumeCodeTable').text = str(data[2])      
    
    #Set the value for the rest of the Structure sub-elements
    root.find('.//Structures//Structure['+str(idx)+']//TypeIndex').text = str(data[2])
    root.find('.//Structures//Structure['+str(idx)+']//ColorAndStyle').text = str(data[3])
    root.find('.//Structures//Structure['+str(idx)+']//SearchCTLow').set('xsi:nil', 'true')
    root.find('.//Structures//Structure['+str(idx)+']//SearchCTHigh').set('xsi:nil', 'true')
    root.find('.//Structures//Structure['+str(idx)+']//DVHLineStyle').text = str(data[4])
    root.find('.//Structures//Structure['+str(idx)+']//DVHLineColor').text = str(data[5])
    root.find('.//Structures//Structure['+str(idx)+']//DVHLineWidth').text = str(data[6])
    root.find('.//Structures//Structure['+str(idx)+']//EUDAlpha').set('xsi:nil', 'true')
    root.find('.//Structures//Structure['+str(idx)+']//TCPAlpha').set('xsi:nil', 'true')
    root.find('.//Structures//Structure['+str(idx)+']//TCPBeta').set('xsi:nil', 'true')
    root.find('.//Structures//Structure['+str(idx)+']//TCPGamma').set('xsi:nil', 'true')
    
    tree.write(filename)

